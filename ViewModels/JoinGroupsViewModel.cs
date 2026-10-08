using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Member "Discover" tab: search the active groups, join one, or open one you already joined.
public partial class JoinGroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IMembershipService _membershipService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private List<JoinableGroup> _allGroups = new List<JoinableGroup>();
    private bool _membershipKnown = true;
    private bool _hasLoaded;

    public ObservableCollection<JoinGroupItem> Groups { get; } = new ObservableCollection<JoinGroupItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasNoGroups;

    // "12 groups  -  3 joined" or "2 of 12 groups" while searching.
    [ObservableProperty]
    private string _summaryText = string.Empty;

    // Empty state: different words for "nothing exists" and "nothing matches your search".
    [ObservableProperty]
    private string _emptyTitle = string.Empty;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    // True only for the very first load, so a refresh or a join never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    // A soft warning (not an error): the list works, but something non-essential failed.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _noticeMessage;

    public bool HasNotice
    {
        get { return !string.IsNullOrWhiteSpace(NoticeMessage); }
    }

    public JoinGroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IMembershipService membershipService,
        IDialogService dialogs,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _membershipService = membershipService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears.
    public async Task LoadAsync()
    {
        if (_session.CurrentProfile == null || _session.IsAdmin)
        {
            // Discover is a tab now (not a pushed page), so there is nothing to go back to.
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            OperationResult<BrowseGroupsResult> result = await _membershipService.GetBrowseGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            BrowseGroupsResult browse = result.Value ?? new BrowseGroupsResult();
            _allGroups = browse.Groups;
            _membershipKnown = browse.MembershipKnown;

            if (browse.MembershipKnown)
            {
                NoticeMessage = null;
            }
            else
            {
                NoticeMessage = "We could not check which groups you have already joined. You can still search and join groups.";
            }

            _hasLoaded = true;
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
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

    private async Task JoinAsync(JoinGroupItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            if (item.IsMember)
            {
                await OpenAsync(item);
                return;
            }

            bool confirmed = await _dialogs.ConfirmAsync(
                "Join group",
                "Join \"" + item.Name + "\"?",
                "Join",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            bool joined;
            try
            {
                OperationResult result = await _membershipService.JoinGroupAsync(item.Id);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.ErrorMessage;
                    joined = false;
                }
                else
                {
                    joined = true;
                }
            }
            finally
            {
                IsBusy = false;
            }

            if (joined)
            {
                _toasts.Show("Joined " + item.Name);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("JOIN GROUP ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    // A joined group: open its announcements (the member announcements feed).
    private async Task OpenAsync(JoinGroupItem item)
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

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Groups.Clear();
        for (int i = 0; i < _allGroups.Count; i++)
        {
            if (Matches(_allGroups[i].Group, term))
            {
                Groups.Add(JoinGroupItem.FromJoinable(_allGroups[i], JoinAsync, OpenAsync));
            }
        }

        HasNoGroups = _hasLoaded && Groups.Count == 0;

        if (!_hasLoaded)
        {
            SummaryText = string.Empty;
            return;
        }

        SummaryText = BuildSummary(term.Length > 0);

        if (_allGroups.Count == 0)
        {
            EmptyTitle = "No groups yet";
            EmptyHint = "Groups created by an admin will show up here.";
        }
        else
        {
            EmptyTitle = "No groups found";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
    }

    private string BuildSummary(bool searching)
    {
        if (searching)
        {
            return Groups.Count + " of " + _allGroups.Count + " groups";
        }

        string total = _allGroups.Count == 1 ? "1 group" : _allGroups.Count + " groups";
        if (!_membershipKnown)
        {
            return total;
        }

        int joined = 0;
        for (int i = 0; i < _allGroups.Count; i++)
        {
            if (_allGroups[i].IsMember)
            {
                joined++;
            }
        }

        return total + "  -  " + joined + " joined";
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
}
