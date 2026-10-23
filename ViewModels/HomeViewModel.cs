using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.ApplicationModel;

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
    private readonly IMemberFeedService _memberFeed;
    private readonly IAdminStatsWatcher _statsWatcher;

    // The community structure: admin counts and the member headings above the announcements.
    private CommunityDirectory? _directory;
    private DateTime _directoryLoadedUtc = DateTime.MinValue;

    // The communities and super communities (headings) rarely change, so they are re-read this often
    // (and at once on pull-to-refresh) instead of every 2 minutes.
    private const int DirectoryCacheMinutes = 10;

    private List<AnnouncementGroup> _memberGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    // Admin: the first few groups. Member: my groups filtered by the search box.
    // Member: the latest announcement of each joined group, newest first.
    public ObservableCollection<MemberAnnouncementItem> MemberAnnouncements { get; } = new ObservableCollection<MemberAnnouncementItem>();

    [ObservableProperty]
    private bool _hasNoMemberAnnouncements;

    // Member: the same cards as MemberAnnouncements, split under community / super community headings.
    public ObservableCollection<MemberHomeSection> MemberSections { get; } = new ObservableCollection<MemberHomeSection>();

    public BulkObservableCollection<HomeGroupItem> AdminGroups { get; } = new BulkObservableCollection<HomeGroupItem>();
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
        ICommunityService communityService,
        IMemberFeedService memberFeed,
        IAdminStatsWatcher statsWatcher)
    {
        _statsWatcher = statsWatcher;
        _memberFeed = memberFeed;
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

    // What one joined group shows on a card: its latest announcement and its planned outage.
    private sealed class GroupState
    {
        public AnnouncementGroup Group = new AnnouncementGroup();
        public Announcement? Latest;
        public bool AnnouncementOk;
        public Outage? Outage;
        public bool OutageOk;
    }

    // Phase 2: members are served by live listeners. If they cannot start (for example the new rules are not
    // deployed yet) Home falls back to the old way (reads every 20 seconds, driven by the page timer).
    private bool _usePolling;

    // True while Home has to be refreshed by a timer (admins always; members only in the fallback).
    // Phase 3: admins are served by the live stats listener too; they only poll when it cannot start.
    private bool _adminPolling;

    public bool UsesPolling
    {
        get { return IsAdmin ? _adminPolling : _usePolling; }
    }

    // Admin: what the live ordering is built from (no reads when a stats document changes).
    private List<AnnouncementGroup> _adminGroups = new List<AnnouncementGroup>();
    private bool _adminHooked;
    private int _adminRebuildToken;

    // Smooth Home: the group cards are rebuilt only when what they show really changed. Coming back from another
    // screen with nothing new does no list work (rebuilding the cards used to freeze the screen for a moment).
    private string _adminListSignature = string.Empty;

    // The pending admin request count is read at most once a minute (and again after the Requests screen was opened
    // or on pull-to-refresh), not on every return to Home.
    private DateTime _pendingCheckedUtc = DateTime.MinValue;
    private int _pendingCount;
    private bool _forceAdminRefresh;
    private const int PendingRequestsMaxAgeSeconds = 60;


    private bool _listenersHooked;
    private string? _liveIdsKey;
    private bool _forceReload;
    private bool _reloadRequested;
    private int _rebuildToken;
    private string _lastSignature = string.Empty;

    // Groups whose feed document does not exist (or cannot be read): read the old way, once, and remember it.
    private readonly Dictionary<string, GroupState> _fallbackStates = new Dictionary<string, GroupState>();

    // Polling mode: one small request per active joined group; only the latest announcement of each is kept.
    private async Task LoadMemberAnnouncementsAsync()
    {
        List<AnnouncementGroup> groups = ActiveMemberGroups();

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

        List<GroupState> states = new List<GroupState>();
        for (int i = 0; i < groups.Count; i++)
        {
            states.Add(StateFromReads(groups[i], tasks[i].Result, outageTasks[i].Result));
        }

        ApplyMemberStates(groups, states);
    }

    private static GroupState StateFromReads(AnnouncementGroup group, OperationResult<List<Announcement>> result, OperationResult<Outage?> outageResult)
    {
        GroupState state = new GroupState();
        state.Group = group;
        state.OutageOk = outageResult.IsSuccess;
        state.Outage = outageResult.IsSuccess ? outageResult.Value : null;
        state.AnnouncementOk = result.IsSuccess && result.Value != null;
        if (state.AnnouncementOk)
        {
            // Newest first.
            state.Latest = result.Value!.Count > 0 ? result.Value[0] : null;
        }

        return state;
    }

    private List<AnnouncementGroup> ActiveMemberGroups()
    {
        List<AnnouncementGroup> groups = new List<AnnouncementGroup>();
        for (int i = 0; i < _memberGroups.Count; i++)
        {
            if (_memberGroups[i].IsActive)
            {
                groups.Add(_memberGroups[i]);
            }
        }

        return groups;
    }

    // The cards built for one group, with the signature of what they were built from.
    private sealed class CardCacheEntry
    {
        public string Signature = string.Empty;
        public List<MemberAnnouncementItem> Cards = new List<MemberAnnouncementItem>();
    }

    private Dictionary<string, CardCacheEntry> _cardCache = new Dictionary<string, CardCacheEntry>();

    // Builds the cards from what is known about each group. No reads happen here, except that a card that is
    // NEW (its group changed) loads its picture and its panic button state. A group whose data did not change keeps
    // its card, so a change in one group does not make every card read again.
    private void ApplyMemberStates(List<AnnouncementGroup> groups, List<GroupState> states)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        _lastSignature = SignatureOf(states, now);

        List<MemberAnnouncementItem> items = new List<MemberAnnouncementItem>();
        List<MemberAnnouncementItem> fresh = new List<MemberAnnouncementItem>();
        Dictionary<string, CardCacheEntry> nextCache = new Dictionary<string, CardCacheEntry>();
        for (int i = 0; i < states.Count; i++)
        {
            GroupState state = states[i];
            string signature = StateSignature(state, now);

            CardCacheEntry? cached;
            List<MemberAnnouncementItem> cards;
            if (_cardCache.TryGetValue(state.Group.Id, out cached) && cached.Signature == signature)
            {
                cards = cached.Cards;
            }
            else
            {
                cards = BuildCards(state, now);
                fresh.AddRange(cards);
            }

            CardCacheEntry entry = new CardCacheEntry();
            entry.Signature = signature;
            entry.Cards = cards;
            nextCache[state.Group.Id] = entry;
            items.AddRange(cards);
        }

        _cardCache = nextCache;

        // A member who has not joined any (active) group still sees the 5-minute message.
        if (groups.Count == 0)
        {
            items.Add(MemberAnnouncementItem.FromGeneralNotice());
        }

        // Newest message first; the 5-minute notices (oldest possible time) end up below the real messages.
        items = items.OrderByDescending(x => x.CreatedAt).ToList();

        // One "leave group" icon per group, on the first card of that group.
        for (int i = 0; i < items.Count; i++)
        {
            items[i].ShowLeave = false;
            items[i].LeaveCommand = null;
        }

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

        // Images arrive a moment later, so the cards show up first. Only new cards load anything.
        for (int i = 0; i < fresh.Count; i++)
        {
            _ = fresh[i].LoadImageAsync(_attachments);
            _ = fresh[i].Extras?.LoadAsync();
        }
    }

    // The cards of ONE group: an outage card (when one is showing) and the announcement or the 5-minute notice.
    private List<MemberAnnouncementItem> BuildCards(GroupState state, DateTimeOffset now)
    {
        List<MemberAnnouncementItem> cards = new List<MemberAnnouncementItem>();
        AnnouncementGroup group = state.Group;

        // A planned outage is shown next to the group's other message, not instead of it.
        if (state.OutageOk && state.Outage != null && OutageText.IsShownAt(state.Outage, now))
        {
            cards.Add(MemberAnnouncementItem.FromOutage(group, state.Outage, now));
        }

        if (!state.AnnouncementOk)
        {
            // Could not load: show nothing for this group rather than a wrong "wait 5 minutes".
            return cards;
        }

        // A completed message disappears and the 5-minute notice comes back.
        Announcement? latest = state.Latest;
        if (latest != null && latest.Status != AnnouncementStatus.Completed)
        {
            MemberAnnouncementItem card = MemberAnnouncementItem.From(group, latest, _attachments);
            card.Extras = new AnnouncementExtras(group.Id, latest, false, _announcementService, _panicService, _dialogs, _toasts);
            cards.Add(card);
        }
        else
        {
            MemberAnnouncementItem notice = MemberAnnouncementItem.FromFiveMinuteNotice(group);
            notice.Extras = AnnouncementExtras.ForNotice(group.Id, _panicService, _dialogs, _toasts);
            cards.Add(notice);
        }

        return cards;
    }

    // Changes only when what the member sees for this group changes: its name, a message, a status,
    // or an outage coming into / going out of view.
    private static string StateSignature(GroupState state, DateTimeOffset now)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append(state.Group.Id).Append('|').Append(state.Group.Name).Append('|').Append(state.Group.Number).Append('|');
        text.Append(state.AnnouncementOk ? "a" : "x");
        if (state.Latest != null)
        {
            text.Append(state.Latest.Id).Append('|').Append(state.Latest.Status).Append('|').Append(state.Latest.Title).Append('|').Append(state.Latest.Message);
            text.Append('|').Append(state.Latest.IsEdited).Append(state.Latest.ResolveMinutes).Append(state.Latest.AttachmentName);
            text.Append('|').Append(state.Latest.CreatedAt.ToUnixTimeMilliseconds());
        }

        text.Append('#');
        if (state.OutageOk && state.Outage != null && OutageText.IsShownAt(state.Outage, now))
        {
            text.Append('o').Append(state.Outage.Status).Append(state.Outage.StartMs).Append(state.Outage.EndMs).Append(state.Outage.PublishAtMs).Append(state.Outage.CancelledAtMs);
        }

        return text.ToString();
    }

    private static string SignatureOf(List<GroupState> states, DateTimeOffset now)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        for (int i = 0; i < states.Count; i++)
        {
            text.Append(StateSignature(states[i], now)).Append(';');
        }

        return text.ToString();
    }

    // ---- Phase 2: live listeners ----

    private async Task LoadMemberLiveAsync(string uid)
    {
        List<string> memberIds = await _memberFeed.StartAsync(uid);
        string idsKey = KeyOf(memberIds);
        HookListeners();

        bool force = _forceReload;
        _forceReload = false;
        if (force)
        {
            // Pull-to-refresh: re-check the group list against the members collection (repairs a missed update).
            await _membershipService.ReconcileGroupsAsync();
            _fallbackStates.Clear();
        }

        // Nothing is read when the member's groups are the same as the last time (for example Home appearing again).
        if (!force && _hasLoaded && string.Equals(_liveIdsKey, idsKey, StringComparison.Ordinal))
        {
            await TickLocalAsync();
            return;
        }

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
        if (_directory == null || force || (DateTime.UtcNow - _directoryLoadedUtc).TotalMinutes >= DirectoryCacheMinutes)
        {
            OperationResult<CommunityDirectory> directoryResult = await _communityService.GetDirectoryAsync();
            if (directoryResult.IsSuccess && directoryResult.Value != null)
            {
                _directory = directoryResult.Value;
                _directoryLoadedUtc = DateTime.UtcNow;
            }
        }

        List<AnnouncementGroup> active = ActiveMemberGroups();
        List<string> activeIds = new List<string>();
        for (int i = 0; i < active.Count; i++)
        {
            activeIds.Add(active[i].Id);
        }

        _memberFeed.WatchGroups(activeIds);
        _liveIdsKey = idsKey;
        await _memberFeed.WaitForFirstAnswersAsync(TimeSpan.FromSeconds(6));
        await RebuildLiveAsync();
    }

    private static string KeyOf(List<string> ids)
    {
        List<string> sorted = new List<string>(ids);
        sorted.Sort(StringComparer.Ordinal);
        return string.Join(",", sorted);
    }

    private void HookListeners()
    {
        if (_listenersHooked)
        {
            return;
        }

        _listenersHooked = true;
        _memberFeed.FeedChanged += OnFeedChanged;
        _memberFeed.GroupsChanged += OnGroupsChanged;
    }

    // Listener callbacks arrive on any thread.
    private void OnFeedChanged()
    {
        MainThread.BeginInvokeOnMainThread(() => _ = ScheduleRebuildAsync());
    }

    private void OnGroupsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() => _ = ReloadAfterGroupsChangedAsync());
    }

    private async Task ReloadAfterGroupsChangedAsync()
    {
        if (!IsMember || _usePolling)
        {
            return;
        }

        _liveIdsKey = null;
        await LoadAsync();
    }

    // Several documents often change together (a post to many groups): one rebuild after a short pause.
    private async Task ScheduleRebuildAsync()
    {
        if (!IsMember || _usePolling || !_hasLoaded)
        {
            return;
        }

        int token = ++_rebuildToken;
        await Task.Delay(250);
        if (token != _rebuildToken)
        {
            return;
        }

        await RebuildLiveAsync();
    }

    private async Task RebuildLiveAsync()
    {
        List<AnnouncementGroup> groups = ActiveMemberGroups();
        List<GroupState> states = await CollectLiveStatesAsync(groups);
        ApplyMemberStates(groups, states);
    }

    private async Task<List<GroupState>> CollectLiveStatesAsync(List<AnnouncementGroup> groups)
    {
        List<GroupState> states = new List<GroupState>();
        for (int i = 0; i < groups.Count; i++)
        {
            AnnouncementGroup group = groups[i];
            GroupFeedDocument? feed;
            FeedState feedState = _memberFeed.GetState(group.Id, out feed);
            if (feedState == FeedState.Ready && feed != null)
            {
                GroupState ready = new GroupState();
                ready.Group = group;
                ready.Latest = feed.ToAnnouncement(group.Id);
                ready.AnnouncementOk = true;
                ready.Outage = feed.ToOutage(group.Id);
                ready.OutageOk = true;
                states.Add(ready);
                continue;
            }

            // No feed document yet (or no answer): read this group the old way. Remembered for Missing / Failed.
            GroupState? known;
            if (feedState != FeedState.Unknown && _fallbackStates.TryGetValue(group.Id, out known))
            {
                known.Group = group;
                states.Add(known);
                continue;
            }

            Task<OperationResult<List<Announcement>>> announcementTask = _announcementService.GetForGroupAsync(group.Id);
            Task<OperationResult<Outage?>> outageTask = _outageService.GetAsync(group.Id);
            await Task.WhenAll(announcementTask, outageTask);
            GroupState read = StateFromReads(group, announcementTask.Result, outageTask.Result);
            if (feedState != FeedState.Unknown)
            {
                _fallbackStates[group.Id] = read;
            }

            states.Add(read);
        }

        return states;
    }

    // Called every 30 seconds by the page while Home is visible. No reads: it only re-evaluates the cards
    // against the clock, so a planned outage appears / disappears on time without anything changing on the server.
    public async Task TickLocalAsync()
    {
        if (!IsMember || _usePolling || !_hasLoaded)
        {
            return;
        }

        List<AnnouncementGroup> groups = ActiveMemberGroups();
        for (int i = 0; i < groups.Count; i++)
        {
            GroupFeedDocument? feed;
            FeedState feedState = _memberFeed.GetState(groups[i].Id, out feed);
            if (feedState == FeedState.Unknown)
            {
                return;
            }
        }

        List<GroupState> states = await CollectLiveStatesAsync(groups);
        if (!string.Equals(SignatureOf(states, DateTimeOffset.UtcNow), _lastSignature, StringComparison.Ordinal))
        {
            ApplyMemberStates(groups, states);
        }
    }

    // Called by the page every time it appears, so counts and lists are fresh after edits elsewhere.
    public async Task LoadAsync()
    {
        using (PerfTimer.Start("Home.Load"))
        {
            await LoadUntimedAsync();
        }
    }

    private async Task LoadUntimedAsync()
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
            // A change arrived while a load was running: load once more when it is finished.
            _reloadRequested = true;
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

        if (_reloadRequested)
        {
            _reloadRequested = false;
            await LoadUntimedAsync();
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
        // The service calls run on a background thread (reading and parsing the directory is not UI work);
        // everything that touches the screen below runs on the UI thread again after each await.
        bool force = _forceAdminRefresh;
        _forceAdminRefresh = false;

        // Phase 5: the count comes from the last time the user list was read (Users tab / add members), kept on the
        // phone. Home itself never reads the users: that was 1 read per user (5,000 users = 5,000 reads).
        OperationResult<List<AnnouncementGroup>> groupsResult = await Task.Run(() => _groupAdminService.GetAllGroupsAsync());

        if (groupsResult.IsSuccess)
        {
            List<AnnouncementGroup> groups = groupsResult.Value ?? new List<AnnouncementGroup>();
            GroupsCountText = groups.Count.ToString();

            // Active groups first (the service already sorted by name; OrderBy keeps that order).
            // Panic reports and fresh messages move a group to the top of the list.
            _adminGroups = groups;
            List<string> groupIds = new List<string>();
            for (int i = 0; i < groups.Count; i++)
            {
                groupIds.Add(groups[i].Id);
            }

            Dictionary<string, GroupActivity> activity = await Task.Run(() => _panicService.GetLatestActivityAsync(groupIds));
            _adminPolling = !_statsWatcher.IsLive;
            HookAdminStats();
            ApplyAdminList(groups, activity);

            HasNoAdminGroups = groups.Count == 0;
            _hasLoaded = true;
        }
        else
        {
            GroupsCountText = "-";
            ErrorMessage = groupsResult.ErrorMessage;
        }

        int? knownUsers = _userAdminService.LastKnownUserCount();
        UsersCountText = knownUsers == null ? "-" : knownUsers.Value.ToString();

        // Not worth a banner when this fails: the rest of Home still works.
        OperationResult<CommunityDirectory> communitiesResult = await Task.Run(() => _communityService.GetDirectoryAsync());
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

        bool pendingStale = (DateTime.UtcNow - _pendingCheckedUtc).TotalSeconds > PendingRequestsMaxAgeSeconds;
        if (force || pendingStale)
        {
            OperationResult<List<AdminRequest>> requestsResult = await Task.Run(() => _adminRequests.GetPendingAsync());
            if (requestsResult.IsSuccess && requestsResult.Value != null)
            {
                _pendingCount = requestsResult.Value.Count;
                _pendingCheckedUtc = DateTime.UtcNow;
            }
            else
            {
                _pendingCount = 0;
            }
        }

        HasPendingRequests = _pendingCount > 0;
        PendingRequestsText = _pendingCount == 1 ? "1 person wants to be an admin" : _pendingCount + " people want to be admins";
    }

    // Orders the groups and fills the Home list from the groups and their stats. Reads nothing.
    private void ApplyAdminList(List<AnnouncementGroup> groups, Dictionary<string, GroupActivity> activity)
    {
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
        int shown = Math.Min(ordered.Count, MaxHomeGroups);
        HasMoreAdminGroups = ordered.Count > shown;
        ViewAllGroupsText = "View all " + ordered.Count + " groups";

        // Nothing the cards show has changed (the usual case when coming back from another screen): leave them alone.
        string signature = BuildAdminListSignature(ordered, shown, panicCounts);
        if (string.Equals(signature, _adminListSignature, StringComparison.Ordinal) && AdminGroups.Count == shown)
        {
            return;
        }

        _adminListSignature = signature;

        List<HomeGroupItem> cards = new List<HomeGroupItem>();
        for (int i = 0; i < shown; i++)
        {
            cards.Add(HomeGroupItem.FromGroup(ordered[i], OpenAnnouncementsAsync, panicCounts.TryGetValue(ordered[i].Id, out int panics) ? panics : 0));
        }

        AdminGroups.ReplaceAll(cards);
    }

    // Everything a Home group card shows (and its order), as one string.
    private static string BuildAdminListSignature(List<AnnouncementGroup> ordered, int shown, Dictionary<string, int> panicCounts)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append(ordered.Count).Append('|').Append(shown);
        for (int i = 0; i < shown; i++)
        {
            AnnouncementGroup group = ordered[i];
            int panics;
            panicCounts.TryGetValue(group.Id, out panics);
            text.Append('|').Append(group.Id)
                .Append('~').Append(group.Name)
                .Append('~').Append(group.Number)
                .Append('~').Append(group.Description)
                .Append('~').Append(group.IsActive ? '1' : '0')
                .Append('~').Append(panics);
        }

        return text.ToString();
    }

    private void HookAdminStats()
    {
        if (_adminHooked)
        {
            return;
        }

        _adminHooked = true;
        _statsWatcher.Changed += OnAdminStatsChanged;
    }

    // Listener callbacks arrive on any thread.
    private void OnAdminStatsChanged()
    {
        MainThread.BeginInvokeOnMainThread(() => _ = ScheduleAdminRebuildAsync());
    }

    // Several stats change together (a post to many groups): one re-order after a short pause. No reads.
    private async Task ScheduleAdminRebuildAsync()
    {
        if (!IsAdmin || !_hasLoaded || !_statsWatcher.IsLive)
        {
            return;
        }

        int token = ++_adminRebuildToken;
        await Task.Delay(300);
        if (token != _adminRebuildToken)
        {
            return;
        }

        List<string> ids = new List<string>();
        for (int i = 0; i < _adminGroups.Count; i++)
        {
            ids.Add(_adminGroups[i].Id);
        }

        // Live: answered from the listener's copy, nothing is read.
        Dictionary<string, GroupActivity> activity = await _panicService.GetLatestActivityAsync(ids);
        if (token == _adminRebuildToken)
        {
            ApplyAdminList(_adminGroups, activity);
        }
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
        // The admin may approve or decline there: read the count again when Home comes back.
        _pendingCheckedUtc = DateTime.MinValue;
        await _navigation.GoToAsync(Routes.AdminRequests);
    }

    private async Task LoadMemberAsync()
    {
        string uid = _session.CurrentProfile?.Id ?? string.Empty;
        if (!_usePolling && uid.Length > 0)
        {
            try
            {
                await LoadMemberLiveAsync(uid);
                return;
            }
            catch (Exception ex)
            {
                // The listeners could not start (for example the new rules are not deployed yet): use the old way.
                System.Diagnostics.Debug.WriteLine("LIVE HOME FAILED, USING POLLING: " + ex);
                _usePolling = true;
                _memberFeed.Stop();
            }
        }

        await LoadMemberPollingAsync();
    }

    private async Task LoadMemberPollingAsync()
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
        if (_directory == null || (DateTime.UtcNow - _directoryLoadedUtc).TotalMinutes >= DirectoryCacheMinutes)
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
            _directoryLoadedUtc = DateTime.MinValue;
            _forceReload = true;
            _forceAdminRefresh = true;
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
