using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin Groups tab. Every group is a card with icon buttons (announcements, members, edit,
// activate / deactivate). The old "tap a row, pick from a list" dialog is gone.
[QueryProperty(nameof(SegmentQuery), "segment")]
public partial class GroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;
    private readonly ICommunityAdminService _communityService;

    private List<Community> _allCommunities = new List<Community>();
    private List<SuperCommunity> _allSupers = new List<SuperCommunity>();

    private List<AnnouncementGroup> _allGroups = new List<AnnouncementGroup>();
    private readonly IPanicService _panicService;
    private Dictionary<string, int> _panicCounts = new Dictionary<string, int>();
    private bool _hasLoaded;

    // Smooth Groups tab: the group cards are rebuilt only when what they show (or the search) changed.
    private string _groupsSignature = string.Empty;

    public BulkObservableCollection<GroupListItem> Groups { get; } = new BulkObservableCollection<GroupListItem>();

    // The cards shown while the switcher is on Communities or Super communities.
    public BulkObservableCollection<CommunityListItem> Communities { get; } = new BulkObservableCollection<CommunityListItem>();

    // Set by Shell when Home opens this tab on a view: admingroups?segment=1 (0 groups, 1 communities, 2 super communities).
    // The setter always applies, so tapping the same Home tile twice still switches the view back.
    public string? SegmentQuery
    {
        get { return null; }
        set
        {
            int parsed;
            if (int.TryParse(value, out parsed) && parsed >= 0 && parsed <= 2)
            {
                Mode = parsed;
            }
        }
    }

    // The switcher at the top: 0 = Groups, 1 = Communities, 2 = Super communities.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGroupsMode))]
    [NotifyPropertyChangedFor(nameof(IsNotGroupsMode))]
    [NotifyPropertyChangedFor(nameof(IsCommunitiesMode))]
    [NotifyPropertyChangedFor(nameof(IsNotCommunitiesMode))]
    [NotifyPropertyChangedFor(nameof(IsSuperMode))]
    [NotifyPropertyChangedFor(nameof(IsNotSuperMode))]
    [NotifyPropertyChangedFor(nameof(HeaderTitle))]
    [NotifyPropertyChangedFor(nameof(NewButtonText))]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholder))]
    private int _mode;

    public bool IsGroupsMode { get { return Mode == 0; } }
    public bool IsNotGroupsMode { get { return Mode != 0; } }
    public bool IsCommunitiesMode { get { return Mode == 1; } }
    public bool IsNotCommunitiesMode { get { return Mode != 1; } }
    public bool IsSuperMode { get { return Mode == 2; } }
    public bool IsNotSuperMode { get { return Mode != 2; } }

    public string HeaderTitle
    {
        get { return Mode == 1 ? "Communities" : Mode == 2 ? "Super communities" : "Groups"; }
    }

    public string NewButtonText
    {
        get { return Mode == 1 ? "New community" : Mode == 2 ? "New super" : "New group"; }
    }

    public string SearchPlaceholder
    {
        get { return Mode == 0 ? "Search by name or description" : "Search by name, number or description"; }
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasNoGroups;

    // "5 groups  -  3 active", or "2 of 5 groups" while searching.
    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _emptyTitle = string.Empty;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    // True only for the very first load, so a refresh or a toggle never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    private readonly IAudiencePickerService _audiencePicker;

    public GroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        IDialogService dialogs,
        IToastService toasts,
        IPanicService panicService,
        ICommunityAdminService communityService,
        IAudiencePickerService audiencePicker)
    {
        _audiencePicker = audiencePicker;
        _communityService = communityService;
        _panicService = panicService;
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Runs automatically whenever the switcher changes. The search starts fresh in each view.
    partial void OnModeChanged(int value)
    {
        ErrorMessage = null;
        if (SearchText.Length > 0)
        {
            SearchText = string.Empty;
        }
        else
        {
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void SelectMode(string mode)
    {
        int parsed;
        if (int.TryParse(mode, out parsed) && parsed >= 0 && parsed <= 2)
        {
            Mode = parsed;
        }
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears (so the list is fresh after adding or editing).
    public async Task LoadAsync()
    {
        using (PerfTimer.Start("GroupsTab.Load"))
        {
            await LoadUntimedAsync();
        }
    }

    private async Task LoadUntimedAsync()
    {
        if (!_session.IsAdmin)
        {
            // Not an admin: never show this page.
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
            OperationResult<List<AnnouncementGroup>> result = await _groupService.GetAllGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _allGroups = result.Value ?? new List<AnnouncementGroup>();

            // Admin: how many people pressed the panic button on each group's latest announcement.
            List<string> groupIds = new List<string>();
            for (int i = 0; i < _allGroups.Count; i++)
            {
                groupIds.Add(_allGroups[i].Id);
            }

            _panicCounts = await _panicService.GetLatestCountsAsync(groupIds);
            await LoadCommunityDataAsync();
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

    // Loads communities and super communities. A failure here must never break the Groups view,
    // so the error is only shown while the switcher is on a community view.
    private async Task LoadCommunityDataAsync()
    {
        OperationResult<List<Community>> communities = await _communityService.GetAllCommunitiesAsync();
        OperationResult<List<SuperCommunity>> supers = await _communityService.GetAllSuperCommunitiesAsync();

        if (communities.IsSuccess && communities.Value != null)
        {
            _allCommunities = communities.Value;
        }

        if (supers.IsSuccess && supers.Value != null)
        {
            _allSupers = supers.Value;
        }

        if (Mode != 0)
        {
            if (!communities.IsSuccess)
            {
                ErrorMessage = communities.ErrorMessage;
            }
            else if (!supers.IsSuccess)
            {
                ErrorMessage = supers.ErrorMessage;
            }
        }
    }

    // The Structure screen: the whole hierarchy as one tree.
    [RelayCommand]
    private Task OpenStructureAsync()
    {
        return _navigation.GoToAsync(Routes.Structure);
    }

    // The one main action on this screen. What it creates depends on the switcher.
    [RelayCommand]
    private Task AddGroupAsync()
    {
        if (Mode == 1)
        {
            return _navigation.GoToAsync(Routes.CommunityEdit + "?kind=" + CommunityEditViewModel.KindCommunity);
        }

        if (Mode == 2)
        {
            return _navigation.GoToAsync(Routes.CommunityEdit + "?kind=" + CommunityEditViewModel.KindSuper);
        }

        return _navigation.GoToAsync(Routes.GroupEdit);
    }

    private async Task EditCommunityAsync(CommunityListItem item)
    {
        try
        {
            string kind = Mode == 2 ? CommunityEditViewModel.KindSuper : CommunityEditViewModel.KindCommunity;
            string route = Routes.CommunityEdit + "?kind=" + kind + "&communityId=" + Uri.EscapeDataString(item.Id);
            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("EDIT COMMUNITY ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    // Admin: write one announcement and post it to one or more groups.
    [RelayCommand]
    private Task NewAnnouncementAsync()
    {
        return _audiencePicker.StartAsync(Routes.AnnouncementEdit);
    }

    private async Task OpenAnnouncementsAsync(GroupListItem item)
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

    private async Task OpenMembersAsync(GroupListItem item)
    {
        try
        {
            string route = Routes.GroupMembers + "?groupId=" + Uri.EscapeDataString(item.Id);
            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN MEMBERS ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private async Task EditAsync(GroupListItem item)
    {
        try
        {
            string route = Routes.GroupEdit + "?groupId=" + Uri.EscapeDataString(item.Id);
            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("EDIT GROUP ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private async Task ChangeActiveAsync(GroupListItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            bool newValue = !item.IsActive;

            string title;
            string message;
            string accept;
            if (newValue)
            {
                title = "Activate group";
                message = "Activate \"" + item.Name + "\"? Members will be able to see it again.";
                accept = "Activate";
            }
            else
            {
                title = "Deactivate group";
                message = "Deactivate \"" + item.Name + "\"? Members will no longer see it. You can activate it again later.";
                accept = "Deactivate";
            }

            bool confirmed = await _dialogs.ConfirmAsync(title, message, accept, "Cancel");
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            bool saved;
            try
            {
                OperationResult result = await _groupService.SetActiveAsync(item.Id, newValue);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.ErrorMessage;
                    saved = false;
                }
                else
                {
                    saved = true;
                }
            }
            finally
            {
                IsBusy = false;
            }

            if (saved)
            {
                _toasts.Show(newValue ? "Activated " + item.Name : "Deactivated " + item.Name);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CHANGE ACTIVE ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private void ApplyFilter()
    {
        if (Mode == 0)
        {
            ApplyGroupFilter();
        }
        else
        {
            ApplyCommunityFilter();
        }
    }

    // Builds the Communities or Super communities cards for the current search.
    private void ApplyCommunityFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();
        bool superMode = Mode == 2;

        Dictionary<string, string> superNames = new Dictionary<string, string>();
        for (int i = 0; i < _allSupers.Count; i++)
        {
            superNames[_allSupers[i].Id] = _allSupers[i].Name;
        }

        Dictionary<string, string> groupNameById = new Dictionary<string, string>();
        for (int i = 0; i < _allGroups.Count; i++)
        {
            groupNameById[_allGroups[i].Id] = _allGroups[i].Name;
        }

        List<CommunityListItem> built = new List<CommunityListItem>();
        int total;

        if (superMode)
        {
            total = _allSupers.Count;
            for (int i = 0; i < _allSupers.Count; i++)
            {
                SuperCommunity item = _allSupers[i];
                if (!(Matches(item.Name, term) || Matches(item.Number, term) || Matches(item.Description, term)))
                {
                    continue;
                }

                int count = 0;
                for (int c = 0; c < _allCommunities.Count; c++)
                {
                    if (_allCommunities[c].SuperCommunityId == item.Id)
                    {
                        count++;
                    }
                }

                string subtitle = "No. " + item.Number + " - " + (count == 1 ? "1 community" : count + " communities");

                List<string> insideNames = new List<string>();
                for (int c = 0; c < _allCommunities.Count; c++)
                {
                    if (_allCommunities[c].SuperCommunityId == item.Id)
                    {
                        insideNames.Add(_allCommunities[c].Name);
                    }
                }

                built.Add(CommunityListItem.Create(item.Id, item.Name, subtitle, item.Description, EditCommunityAsync, ChipText.Build(insideNames, 3)));
            }
        }
        else
        {
            total = _allCommunities.Count;
            for (int i = 0; i < _allCommunities.Count; i++)
            {
                Community item = _allCommunities[i];
                if (!(Matches(item.Name, term) || Matches(item.Number, term) || Matches(item.Description, term)))
                {
                    continue;
                }

                int count = item.GroupIds == null ? 0 : item.GroupIds.Count;
                string subtitle = "No. " + item.Number + " - " + (count == 1 ? "1 group" : count + " groups");

                string? superName;
                if (!string.IsNullOrEmpty(item.SuperCommunityId) && superNames.TryGetValue(item.SuperCommunityId, out superName))
                {
                    subtitle = subtitle + " - " + superName;
                }

                // The names of the groups inside (ids of groups that no longer exist are skipped).
                List<string> groupNames = new List<string>();
                if (item.GroupIds != null)
                {
                    for (int g = 0; g < item.GroupIds.Count; g++)
                    {
                        string? groupName;
                        if (groupNameById.TryGetValue(item.GroupIds[g], out groupName))
                        {
                            groupNames.Add(groupName);
                        }
                    }
                }

                built.Add(CommunityListItem.Create(item.Id, item.Name, subtitle, item.Description, EditCommunityAsync, ChipText.Build(groupNames, 3)));
            }
        }

        Communities.ReplaceAll(built);

        string noun = superMode ? "super communities" : "communities";
        HasNoGroups = _hasLoaded && Communities.Count == 0;

        if (!_hasLoaded)
        {
            SummaryText = string.Empty;
            return;
        }

        if (term.Length > 0)
        {
            SummaryText = Communities.Count + " of " + total + " " + noun;
        }
        else if (total == 1)
        {
            SummaryText = superMode ? "1 super community" : "1 community";
        }
        else
        {
            SummaryText = total + " " + noun;
        }

        if (total == 0)
        {
            EmptyTitle = superMode ? "No super communities yet" : "No communities yet";
            EmptyHint = superMode ? "Tap New super to create the first one." : "Tap New community to create the first one.";
        }
        else
        {
            EmptyTitle = superMode ? "No super communities found" : "No communities found";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
    }

    private static bool Matches(string? text, string term)
    {
        return term.Length == 0 || ContainsText(text, term);
    }

    private void ApplyGroupFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        int activeCount = 0;
        List<AnnouncementGroup> matching = new List<AnnouncementGroup>();
        System.Text.StringBuilder signature = new System.Text.StringBuilder();
        signature.Append(term);
        for (int i = 0; i < _allGroups.Count; i++)
        {
            AnnouncementGroup group = _allGroups[i];

            if (group.IsActive)
            {
                activeCount++;
            }

            if (Matches(group, term))
            {
                matching.Add(group);
                int panics;
                _panicCounts.TryGetValue(group.Id, out panics);
                signature.Append('|').Append(group.Id)
                    .Append('~').Append(group.Name)
                    .Append('~').Append(group.Number)
                    .Append('~').Append(group.Description)
                    .Append('~').Append(group.IsActive ? '1' : '0')
                    .Append('~').Append(panics);
            }
        }

        // Nothing the cards show has changed (the usual case when the tab is opened again): keep the cards as they are.
        string signatureText = signature.ToString();
        if (!string.Equals(signatureText, _groupsSignature, StringComparison.Ordinal) || Groups.Count != matching.Count)
        {
            _groupsSignature = signatureText;

            List<GroupListItem> cards = new List<GroupListItem>();
            for (int i = 0; i < matching.Count; i++)
            {
                AnnouncementGroup group = matching[i];
                cards.Add(GroupListItem.FromGroup(group, OpenAnnouncementsAsync, OpenMembersAsync, EditAsync, ChangeActiveAsync, _panicCounts.TryGetValue(group.Id, out int panics) ? panics : 0));
            }

            Groups.ReplaceAll(cards);
        }

        HasNoGroups = _hasLoaded && Groups.Count == 0;

        if (!_hasLoaded)
        {
            SummaryText = string.Empty;
            return;
        }

        if (term.Length > 0)
        {
            SummaryText = Groups.Count + " of " + _allGroups.Count + " groups";
        }
        else
        {
            string total = _allGroups.Count == 1 ? "1 group" : _allGroups.Count + " groups";
            SummaryText = total + "  -  " + activeCount + " active";
        }

        if (_allGroups.Count == 0)
        {
            EmptyTitle = "No groups yet";
            EmptyHint = "Tap New group to create the first one.";
        }
        else
        {
            EmptyTitle = "No groups found";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
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
}
