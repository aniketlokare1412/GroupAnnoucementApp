using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// The Home tab for BOTH roles.
//   Admin : counts (groups, users), quick actions, and the first few groups.
//   Member: search box, "My groups" with a bell per group, and a "Find more groups" card.
// It only uses services that already exist. There is no cross-group announcements feed here.
public partial class HomeViewModel : BaseViewModel
{
    private const int AdminPreviewCount = 3;

    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupAdminService;
    private readonly IUserAdminService _userAdminService;
    private readonly IMembershipService _membershipService;
    private readonly IToastService _toasts;

    private List<AnnouncementGroup> _memberGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    // Admin: the first few groups. Member: my groups filtered by the search box.
    public ObservableCollection<HomeGroupItem> AdminGroups { get; } = new ObservableCollection<HomeGroupItem>();
    public ObservableCollection<HomeGroupItem> MyGroups { get; } = new ObservableCollection<HomeGroupItem>();

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isMember;

    // The dash is shown until the number is known (or if it could not be loaded).
    [ObservableProperty]
    private string _groupsCountText = "-";

    [ObservableProperty]
    private string _usersCountText = "-";

    [ObservableProperty]
    private bool _hasNoAdminGroups;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _joinedText = string.Empty;

    [ObservableProperty]
    private bool _hasNoMyGroups;

    // True only for the very first load, so a refresh never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    public HomeViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupAdminService,
        IUserAdminService userAdminService,
        IMembershipService membershipService,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _groupAdminService = groupAdminService;
        _userAdminService = userAdminService;
        _membershipService = membershipService;
        _toasts = toasts;
    }

    // Runs automatically whenever the search text changes (member view only).
    partial void OnSearchTextChanged(string value)
    {
        ApplyMemberFilter();
    }

    // Called by the page every time it appears, so counts and lists are fresh after edits elsewhere.
    public async Task LoadAsync()
    {
        UserProfile? profile = _session.CurrentProfile;
        if (profile == null)
        {
            // Safety net: never show Home without a loaded profile.
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (IsBusy)
        {
            return;
        }

        ApplyProfile(profile);

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            if (IsAdmin)
            {
                await LoadAdminAsync();
            }
            else
            {
                await LoadMemberAsync();
            }
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
    }

    private void ApplyProfile(UserProfile profile)
    {
        bool admin = _session.IsAdmin;
        IsAdmin = admin;
        IsMember = !admin;

        string displayName = string.IsNullOrWhiteSpace(profile.Name) ? profile.Email : profile.Name;
        Initials = InitialsHelper.From(displayName);
        FirstName = BuildFirstName(profile);
        Greeting = BuildGreeting(DateTime.Now);
    }

    private async Task LoadAdminAsync()
    {
        Task<OperationResult<List<AnnouncementGroup>>> groupsTask = _groupAdminService.GetAllGroupsAsync();
        Task<OperationResult<List<UserProfile>>> usersTask = _userAdminService.GetAllUsersAsync();
        await Task.WhenAll(groupsTask, usersTask);

        OperationResult<List<AnnouncementGroup>> groupsResult = groupsTask.Result;
        OperationResult<List<UserProfile>> usersResult = usersTask.Result;

        if (groupsResult.IsSuccess)
        {
            List<AnnouncementGroup> groups = groupsResult.Value ?? new List<AnnouncementGroup>();
            GroupsCountText = groups.Count.ToString();

            // Active groups first (the service already sorted by name; OrderBy keeps that order).
            List<AnnouncementGroup> ordered = groups.OrderByDescending(g => g.IsActive).ToList();

            AdminGroups.Clear();
            int shown = Math.Min(AdminPreviewCount, ordered.Count);
            for (int i = 0; i < shown; i++)
            {
                AdminGroups.Add(HomeGroupItem.FromGroup(ordered[i], OpenAnnouncementsAsync));
            }

            HasNoAdminGroups = groups.Count == 0;
            _hasLoaded = true;
        }
        else
        {
            GroupsCountText = "-";
            ErrorMessage = groupsResult.ErrorMessage;
        }

        if (usersResult.IsSuccess)
        {
            List<UserProfile> users = usersResult.Value ?? new List<UserProfile>();
            UsersCountText = users.Count.ToString();
        }
        else
        {
            // Not worth a banner: the rest of Home still works.
            UsersCountText = "-";
            _toasts.Show("Could not load the user count.");
        }
    }

    private async Task LoadMemberAsync()
    {
        OperationResult<List<AnnouncementGroup>> result = await _membershipService.GetMyGroupsAsync();
        if (!result.IsSuccess)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        _memberGroups = result.Value ?? new List<AnnouncementGroup>();
        _hasLoaded = true;
        JoinedText = _memberGroups.Count + " joined";
        ApplyMemberFilter();
    }

    private void ApplyMemberFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        MyGroups.Clear();
        for (int i = 0; i < _memberGroups.Count; i++)
        {
            AnnouncementGroup group = _memberGroups[i];
            if (Matches(group, term))
            {
                MyGroups.Add(HomeGroupItem.FromGroup(group, OpenAnnouncementsAsync));
            }
        }

        // "Nothing joined" is only true when there is no search term; an empty search result is different.
        HasNoMyGroups = _hasLoaded && _memberGroups.Count == 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    // The avatar at the top. Account is a pushed page, not a tab.
    [RelayCommand]
    private Task OpenAccountAsync()
    {
        return _navigation.GoToAsync(Routes.Account);
    }

    [RelayCommand]
    private Task OpenGroupsAsync()
    {
        return _navigation.GoToAsync(Routes.AdminGroups);
    }

    [RelayCommand]
    private Task OpenUsersAsync()
    {
        return _navigation.GoToAsync(Routes.AdminUsers);
    }

    // Admin main action: write one announcement and post it to one or more groups.
    [RelayCommand]
    private Task NewPostAsync()
    {
        return _navigation.GoToAsync(Routes.AnnouncementEdit);
    }

    [RelayCommand]
    private Task FindMoreGroupsAsync()
    {
        return _navigation.GoToAsync(Routes.MemberDiscover);
    }

    private async Task OpenAnnouncementsAsync(HomeGroupItem item)
    {
        try
        {
            string route = Routes.Announcements
                + "?groupId=" + Uri.EscapeDataString(item.Id)
                + "&groupName=" + Uri.EscapeDataString(item.Name);
            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN ANNOUNCEMENTS ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private static bool Matches(AnnouncementGroup group, string term)
    {
        if (term.Length == 0)
        {
            return true;
        }

        if (ContainsText(group.Name, term))
        {
            return true;
        }

        if (ContainsText(group.Description, term))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsText(string? text, string term)
    {
        if (text == null)
        {
            return false;
        }

        return text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildFirstName(UserProfile profile)
    {
        string source = profile.Name;
        if (string.IsNullOrWhiteSpace(source))
        {
            // Fall back to the part of the email before the @.
            int at = profile.Email.IndexOf('@');
            source = at > 0 ? profile.Email.Substring(0, at) : profile.Email;
        }

        string[] parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0];
    }

    private static string BuildGreeting(DateTime now)
    {
        if (now.Hour < 12)
        {
            return "Good morning";
        }

        if (now.Hour < 17)
        {
            return "Good afternoon";
        }

        return "Good evening";
    }
}
