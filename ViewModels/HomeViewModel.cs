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

    private readonly INavigationService _navigation;
    private readonly IAdminRequestService _adminRequests;
    private readonly IAnnouncementService _announcementService;
    private readonly IAttachmentService _attachments;
    private readonly IPanicService _panicService;
    private readonly IDialogService _dialogs;
    private readonly IOutageService _outageService;
    private Dictionary<string, int>? _previousPanicCounts;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupAdminService;
    private readonly IUserAdminService _userAdminService;
    private readonly IMembershipService _membershipService;
    private readonly IToastService _toasts;
    private readonly ICommunityService _communityService;

    // The community structure: admin counts and the member headings above the announcements.
    private CommunityDirectory? _directory;
    private DateTime _directoryLoadedUtc = DateTime.MinValue;

    private List<AnnouncementGroup> _memberGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    // Admin: the first few groups. Member: my groups filtered by the search box.
    // Member: the latest announcement of each joined group, newest first.
    public ObservableCollection<MemberAnnouncementItem> MemberAnnouncements { get; } = new ObservableCollection<MemberAnnouncementItem>();

    [ObservableProperty]
    private bool _hasNoMemberAnnouncements;

    // Member: the same cards as MemberAnnouncements, split under community / super community headings.
    public ObservableCollection<MemberHomeSection> MemberSections { get; } = new ObservableCollection<MemberHomeSection>();

    public ObservableCollection<HomeGroupItem> AdminGroups { get; } = new ObservableCollection<HomeGroupItem>();
    public ObservableCollection<HomeGroupItem> MyGroups { get; } = new ObservableCollection<HomeGroupItem>();

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _firstName = string.Empty;

    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAdmin))]
    private bool _isAdmin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMember))]
    private bool _isMember;

    // The dash is shown until the number is known (or if it could not be loaded).
    [ObservableProperty]
    private string _groupsCountText = "-";

    [ObservableProperty]
    private string _usersCountText = "-";

    private const int MaxHomeGroups = 5;

    // True when there are more groups than Home lists: shows the "View all" links.
    [ObservableProperty]
    private bool _hasMoreAdminGroups;

    [ObservableProperty]
    private string _viewAllGroupsText = string.Empty;

    [ObservableProperty]
    private string _communitiesCountText = "-";

    [ObservableProperty]
    private string _superCommunitiesCountText = "-";

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
    [NotifyPropertyChangedFor(nameof(ShowAdmin))]
    [NotifyPropertyChangedFor(nameof(ShowMember))]
    private bool _showLoading;

    // Admin / member sections stay hidden behind the skeleton during the first load.
    // Admin: how many people asked to become administrators.
    [ObservableProperty]
    private bool _hasPendingRequests;

    [ObservableProperty]
    private string _pendingRequestsText = string.Empty;

    public bool ShowAdmin => IsAdmin && !ShowLoading;

    public bool ShowMember => IsMember && !ShowLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    public HomeViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupAdminService,
        IUserAdminService userAdminService,
        IMembershipService membershipService,
        IToastService toasts,
        IAdminRequestService adminRequests,
        IAnnouncementService announcementService,
        IAttachmentService attachments,
        IPanicService panicService,
        IDialogService dialogs,
        IOutageService outageService,
        ICommunityService communityService)
    {
        _communityService = communityService;
        _outageService = outageService;
        _panicService = panicService;
        _dialogs = dialogs;
        _attachments = attachments;
        _adminRequests = adminRequests;
        _announcementService = announcementService;
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

    // One small request per active joined group; only the latest announcement of each is kept.
    private async Task LoadMemberAnnouncementsAsync()
    {
        List<AnnouncementGroup> groups = new List<AnnouncementGroup>();
        for (int i = 0; i < _memberGroups.Count; i++)
        {
            if (_memberGroups[i].IsActive)
            {
                groups.Add(_memberGroups[i]);
            }
        }

        List<Task<OperationResult<List<Announcement>>>> tasks = new List<Task<OperationResult<List<Announcement>>>>();
        for (int i = 0; i < groups.Count; i++)
        {
            tasks.Add(_announcementService.GetForGroupAsync(groups[i].Id));
        }

        List<Task<OperationResult<Outage?>>> outageTasks = new List<Task<OperationResult<Outage?>>>();
        for (int i = 0; i < groups.Count; i++)
        {
            outageTasks.Add(_outageService.GetAsync(groups[i].Id));
        }

        await Task.WhenAll(tasks);
        await Task.WhenAll(outageTasks);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<MemberAnnouncementItem> items = new List<MemberAnnouncementItem>();
        for (int i = 0; i < groups.Count; i++)
        {
            // A planned outage is shown next to the group's other message, not instead of it.
            OperationResult<Outage?> outageResult = outageTasks[i].Result;
            if (outageResult.IsSuccess && outageResult.Value != null && OutageText.IsShownAt(outageResult.Value, now))
            {
                items.Add(MemberAnnouncementItem.FromOutage(groups[i], outageResult.Value, now));
            }

            OperationResult<List<Announcement>> result = tasks[i].Result;
            if (!result.IsSuccess || result.Value == null)
            {
                // Could not load: show nothing for this group rather than a wrong "wait 5 minutes".
                continue;
            }

            // Newest first. A completed message disappears and the 5-minute notice comes back.
            Announcement? latest = result.Value.Count > 0 ? result.Value[0] : null;
            if (latest != null && latest.Status != AnnouncementStatus.Completed)
            {
                MemberAnnouncementItem card = MemberAnnouncementItem.From(groups[i], latest, _attachments);
                card.Extras = new AnnouncementExtras(groups[i].Id, latest, false, _announcementService, _panicService, _dialogs, _toasts);
                items.Add(card);
            }
            else
            {
                MemberAnnouncementItem notice = MemberAnnouncementItem.FromFiveMinuteNotice(groups[i]);
                notice.Extras = AnnouncementExtras.ForNotice(groups[i].Id, _panicService, _dialogs, _toasts);
                items.Add(notice);
            }
        }

        // A member who has not joined any (active) group still sees the 5-minute message.
        if (groups.Count == 0)
        {
            items.Add(MemberAnnouncementItem.FromGeneralNotice());
        }

        // Newest message first; the 5-minute notices (oldest possible time) end up below the real messages.
        items = items.OrderByDescending(x => x.CreatedAt).ToList();

        // One "leave group" icon per group, on the first card of that group.
        HashSet<string> withLeave = new HashSet<string>();
        for (int i = 0; i < items.Count; i++)
        {
            string leaveGroupId = items[i].GroupId;
            if (!string.IsNullOrEmpty(leaveGroupId) && withLeave.Add(leaveGroupId))
            {
                string leaveGroupName = items[i].GroupName;
                items[i].ShowLeave = true;
                items[i].LeaveCommand = new AsyncRelayCommand(() => LeaveGroupFromHomeAsync(leaveGroupId, leaveGroupName));
            }
        }

        MemberAnnouncements.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            MemberAnnouncements.Add(items[i]);
        }

        HasNoMemberAnnouncements = MemberAnnouncements.Count == 0;
        BuildMemberSections(items);

        // Images arrive a moment later, so the cards show up first.
        for (int i = 0; i < MemberAnnouncements.Count; i++)
        {
            _ = MemberAnnouncements[i].LoadImageAsync(_attachments);
            _ = MemberAnnouncements[i].Extras?.LoadAsync();
        }
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
            // Panic reports and fresh messages move a group to the top of the list.
            List<string> groupIds = new List<string>();
            for (int i = 0; i < groups.Count; i++)
            {
                groupIds.Add(groups[i].Id);
            }

            Dictionary<string, GroupActivity> activity = await _panicService.GetLatestActivityAsync(groupIds);
            Dictionary<string, int> panicCounts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, GroupActivity> pair in activity)
            {
                panicCounts[pair.Key] = pair.Value.PanicCount;
            }

            List<AnnouncementGroup> ordered = OrderForAdminHome(groups, activity);

            // One beep when any group's panic count has gone up since the last check.
            if (_previousPanicCounts != null)
            {
                foreach (KeyValuePair<string, int> pair in panicCounts)
                {
                    int before;
                    _previousPanicCounts.TryGetValue(pair.Key, out before);
                    if (pair.Value > before)
                    {
                        AlertFeedback.Beep();
                        break;
                    }
                }
            }

            _previousPanicCounts = panicCounts;

            // With a few hundred groups Home shows only the first few; "View all" opens the Groups tab.
            AdminGroups.Clear();
            int shown = Math.Min(ordered.Count, MaxHomeGroups);
            HasMoreAdminGroups = ordered.Count > shown;
            ViewAllGroupsText = "View all " + ordered.Count + " groups";
            for (int i = 0; i < shown; i++)
            {
                AdminGroups.Add(HomeGroupItem.FromGroup(ordered[i], OpenAnnouncementsAsync, panicCounts.TryGetValue(ordered[i].Id, out int panics) ? panics : 0));
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

        // Not worth a banner when this fails: the rest of Home still works.
        OperationResult<CommunityDirectory> communitiesResult = await _communityService.GetDirectoryAsync();
        if (communitiesResult.IsSuccess && communitiesResult.Value != null)
        {
            CommunitiesCountText = communitiesResult.Value.Communities.Count.ToString();
            SuperCommunitiesCountText = communitiesResult.Value.SuperCommunities.Count.ToString();
        }
        else
        {
            CommunitiesCountText = "-";
            SuperCommunitiesCountText = "-";
        }

        OperationResult<List<AdminRequest>> requestsResult = await _adminRequests.GetPendingAsync();
        int pending = 0;
        if (requestsResult.IsSuccess && requestsResult.Value != null)
        {
            pending = requestsResult.Value.Count;
        }

        HasPendingRequests = pending > 0;
        PendingRequestsText = pending == 1 ? "1 person wants to be an admin" : pending + " people want to be admins";
    }

    // The count tiles and "View all" jump to the Groups tab on the right view (0 groups, 1 communities, 2 super communities).
    // 1) active groups first, 2) groups with open panic reports (on the open message or on the 5-minute notice, most reports first),
    // 3) groups the admin posted to most recently, 4) the rest by name.
    private static List<AnnouncementGroup> OrderForAdminHome(List<AnnouncementGroup> groups, Dictionary<string, GroupActivity> activity)
    {
        return groups
            .OrderByDescending(g => g.IsActive)
            .ThenByDescending(g => OpenPanics(activity, g.Id) > 0)
            .ThenByDescending(g => OpenPanics(activity, g.Id))
            .ThenByDescending(g => LastPost(activity, g.Id))
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int OpenPanics(Dictionary<string, GroupActivity> activity, string groupId)
    {
        GroupActivity? item;
        if (activity.TryGetValue(groupId, out item))
        {
            return item.PanicCount;
        }

        return 0;
    }

    private static DateTimeOffset LastPost(Dictionary<string, GroupActivity> activity, string groupId)
    {
        GroupActivity? item;
        return activity.TryGetValue(groupId, out item) ? item.LastPostedAt : DateTimeOffset.MinValue;
    }

    [RelayCommand]
    private Task OpenGroupsTabAsync()
    {
        return _navigation.GoToAsync(Routes.AdminGroups + "?segment=0");
    }

    [RelayCommand]
    private Task OpenCommunitiesTabAsync()
    {
        return _navigation.GoToAsync(Routes.AdminGroups + "?segment=1");
    }

    [RelayCommand]
    private Task OpenSuperCommunitiesTabAsync()
    {
        return _navigation.GoToAsync(Routes.AdminGroups + "?segment=2");
    }

    [RelayCommand]
    private Task OpenUsersTabAsync()
    {
        return _navigation.GoToAsync(Routes.AdminUsers);
    }

    [RelayCommand]
    private async Task OpenAdminRequestsAsync()
    {
        await _navigation.GoToAsync(Routes.AdminRequests);
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

        // The headings are nice to have: if this fails, the announcements show without them.
        // The structure rarely changes, so Home's 20 second refresh reuses it for two minutes.
        if (_directory == null || (DateTime.UtcNow - _directoryLoadedUtc).TotalMinutes >= 2)
        {
            OperationResult<CommunityDirectory> directoryResult = await _communityService.GetDirectoryAsync();
            if (directoryResult.IsSuccess && directoryResult.Value != null)
            {
                _directory = directoryResult.Value;
                _directoryLoadedUtc = DateTime.UtcNow;
            }
        }

        await LoadMemberAnnouncementsAsync();
    }

    private bool _isLeaving;

    // The "leave group" icon on a Home card: same confirmation as on the group page, then Home reloads.
    private async Task LeaveGroupFromHomeAsync(string groupId, string groupName)
    {
        if (_isLeaving)
        {
            return;
        }

        string shownName = string.IsNullOrWhiteSpace(groupName) ? "the group" : groupName;
        bool confirmed = await _dialogs.ConfirmAsync(
            "Leave group",
            "Leave \"" + shownName + "\"? You will no longer see its announcements. You can join again later.",
            "Leave",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        _isLeaving = true;
        try
        {
            OperationResult result = await _membershipService.LeaveGroupAsync(groupId);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _toasts.Show("Left " + shownName);
            await LoadAsync();
        }
        finally
        {
            _isLeaving = false;
        }
    }

    // Orders communities: those inside a super community first (by super community name), then by name.
    private static int CompareCommunities(CommunityDirectory directory, Community a, Community b)
    {
        SuperCommunity? superA = directory.SuperOf(a);
        SuperCommunity? superB = directory.SuperOf(b);
        if (superA != null && superB == null)
        {
            return -1;
        }

        if (superA == null && superB != null)
        {
            return 1;
        }

        if (superA != null && superB != null && superA.Id != superB.Id)
        {
            return string.Compare(superA.Name, superB.Name, StringComparison.OrdinalIgnoreCase);
        }

        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    // Shows every card ONCE, under the first community its group is in (communities inside a super
    // community come first). A group in more than one community shows "Also in: ..." on its first card.
    // Groups in no community go last under "Other groups".
    // With no communities around the member's groups there is one block without headings.
    private void BuildMemberSections(List<MemberAnnouncementItem> items)
    {
        MemberSections.Clear();

        Dictionary<string, List<MemberAnnouncementItem>> byCommunity = new Dictionary<string, List<MemberAnnouncementItem>>();
        List<MemberAnnouncementItem> others = new List<MemberAnnouncementItem>();
        HashSet<string> alsoShown = new HashSet<string>();

        for (int i = 0; i < items.Count; i++)
        {
            List<Community> communities = new List<Community>();
            if (_directory != null)
            {
                communities.AddRange(_directory.CommunitiesOfGroup(items[i].GroupId));
            }

            if (communities.Count == 0)
            {
                others.Add(items[i]);
                continue;
            }

            CommunityDirectory knownDirectory = _directory!;
            communities.Sort((a, b) => CompareCommunities(knownDirectory, a, b));

            // The primary community only: the card is not repeated under the other communities.
            List<MemberAnnouncementItem>? list;
            if (!byCommunity.TryGetValue(communities[0].Id, out list))
            {
                list = new List<MemberAnnouncementItem>();
                byCommunity[communities[0].Id] = list;
            }

            list.Add(items[i]);

            if (communities.Count > 1 && alsoShown.Add(items[i].GroupId))
            {
                List<string> otherNames = new List<string>();
                for (int j = 1; j < communities.Count; j++)
                {
                    otherNames.Add(communities[j].Name);
                }

                items[i].AlsoInText = "Also in: " + string.Join(", ", otherNames);
            }
        }

        if (_directory == null || byCommunity.Count == 0)
        {
            if (items.Count > 0)
            {
                AddSection(string.Empty, string.Empty, items);
            }

            return;
        }

        CommunityDirectory directory = _directory;
        List<Community> used = new List<Community>();
        for (int i = 0; i < directory.Communities.Count; i++)
        {
            if (byCommunity.ContainsKey(directory.Communities[i].Id))
            {
                used.Add(directory.Communities[i]);
            }
        }

        // Communities inside a super community first (by super community name), the others after.
        used.Sort((a, b) => CompareCommunities(directory, a, b));

        string lastSuperName = string.Empty;
        for (int i = 0; i < used.Count; i++)
        {
            SuperCommunity? parent = directory.SuperOf(used[i]);
            string superName = parent == null ? string.Empty : parent.Name;

            // The super community heading only appears above its first community.
            string heading = superName != lastSuperName ? superName : string.Empty;
            lastSuperName = superName;

            AddSection(heading, used[i].Name, byCommunity[used[i].Id]);
        }

        if (others.Count > 0)
        {
            AddSection(string.Empty, "Other groups", others);
        }
    }

    private void AddSection(string superTitle, string title, List<MemberAnnouncementItem> cards)
    {
        MemberHomeSection section = new MemberHomeSection();
        section.SuperTitle = superTitle;
        section.Title = title;
        section.Cards = cards;
        MemberSections.Add(section);
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
    private Task NewGroupAsync()
    {
        return _navigation.GoToAsync(Routes.GroupEdit);
    }

    [RelayCommand]
    private Task NewOutageAsync()
    {
        return _navigation.GoToAsync(Routes.OutageEdit);
    }

    [RelayCommand]
    private Task NewPostAsync()
    {
        return _navigation.GoToAsync(Routes.AnnouncementEdit);
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

        if (ContainsText(group.Number, term))
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
