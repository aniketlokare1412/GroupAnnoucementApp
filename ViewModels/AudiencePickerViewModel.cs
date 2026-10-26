using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// The audience picker: who gets the announcement. Two independent tabs, only the tab you finish on counts:
//  - Groups: search and tick any active group.
//  - Communities: choose ONE super community, then tick its communities. The groups below are the active groups of
//    the ticked communities (each group once) and cannot be edited.
public partial class AudiencePickerViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAudiencePickerService _picker;
    private readonly ICommunityService _communityService;

    private List<AudienceRow> _groupRows = new List<AudienceRow>();
    private HashSet<string> _selected = new HashSet<string>();
    private Dictionary<string, AudienceGroup> _activeGroups = new Dictionary<string, AudienceGroup>();
    private CommunityDirectory? _directory;
    private List<SuperCommunity> _supers = new List<SuperCommunity>();
    private HashSet<string> _communityIds = new HashSet<string>();
    private HashSet<string> _hierarchyGroupIds = new HashSet<string>();
    private int _totalGroups;
    private bool _isLoaded;
    private bool _isFinished;
    private bool _suppressSuperChange;

    // Groups tab rows.
    public ObservableCollection<AudienceRow> Rows { get; } = new ObservableCollection<AudienceRow>();

    // Communities tab: super community dropdown, community tick list, read-only groups.
    public ObservableCollection<string> SuperChoices { get; } = new ObservableCollection<string>();
    public ObservableCollection<CommunityOptionRow> CommunityOptions { get; } = new ObservableCollection<CommunityOptionRow>();
    public ObservableCollection<HierarchyGroupRow> HierarchyGroups { get; } = new ObservableCollection<HierarchyGroupRow>();

    // 0 = Groups, 1 = Communities.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGroupsSegment))]
    [NotifyPropertyChangedFor(nameof(IsNotGroupsSegment))]
    [NotifyPropertyChangedFor(nameof(IsHierarchySegment))]
    [NotifyPropertyChangedFor(nameof(IsNotHierarchySegment))]
    private int _segment;

    public bool IsGroupsSegment => Segment == 0;
    public bool IsNotGroupsSegment => Segment != 0;
    public bool IsHierarchySegment => Segment == 1;
    public bool IsNotHierarchySegment => Segment != 1;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _doneText = "Done";

    [ObservableProperty]
    private bool _hasNoRows;

    [ObservableProperty]
    private string _emptyTitle = string.Empty;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    [ObservableProperty]
    private bool _showLoading;

    // -1 = no super community chosen.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuperSelected))]
    private int _selectedSuperIndex = -1;

    public bool HasSuperSelected => SelectedSuperIndex >= 0 && SelectedSuperIndex < _supers.Count;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelRotation))]
    private bool _isCommunityPanelOpen;

    public double PanelRotation => IsCommunityPanelOpen ? 90 : 0;

    [ObservableProperty]
    private string _communitiesButtonText = "Choose a Feeder first";

    [ObservableProperty]
    private bool _hasNoCommunities;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllCommunitiesOff))]
    private bool _allCommunitiesOn;

    public bool AllCommunitiesOff => !AllCommunitiesOn;

    [ObservableProperty]
    private string _hierarchyHeading = string.Empty;

    [ObservableProperty]
    private bool _hasNoHierarchyGroups;

    [ObservableProperty]
    private string _hierarchyEmptyTitle = string.Empty;

    [ObservableProperty]
    private string _hierarchyEmptyHint = string.Empty;

    public AudiencePickerViewModel(
        INavigationService navigation,
        ISessionService session,
        IAudiencePickerService picker,
        ICommunityService communityService)
    {
        _navigation = navigation;
        _session = session;
        _picker = picker;
        _communityService = communityService;
    }

    partial void OnSearchTextChanged(string value)
    {
        Rebuild();
    }

    // Switching between Groups and Communities starts over: nothing stays picked from the other tab.
    partial void OnSegmentChanged(int value)
    {
        if (_isLoaded)
        {
            ResetChoices();
        }

        Rebuild();
        UpdateSummary();
    }

    private void ResetChoices()
    {
        _selected.Clear();
        SearchText = string.Empty;
        _communityIds.Clear();

        _suppressSuperChange = true;
        SelectedSuperIndex = -1;
        _suppressSuperChange = false;

        IsCommunityPanelOpen = false;
        BuildCommunityOptions();
        RebuildHierarchyGroups();
        RefreshStates();
        ErrorMessage = null;
    }

    // A different super community: its communities replace the old ones and nothing is ticked.
    partial void OnSelectedSuperIndexChanged(int value)
    {
        if (_suppressSuperChange || !_isLoaded)
        {
            return;
        }

        _communityIds.Clear();
        BuildCommunityOptions();
        RebuildHierarchyGroups();
        IsCommunityPanelOpen = CommunityOptions.Count > 0;
    }

    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        if (_isLoaded || IsBusy)
        {
            return;
        }

        AudienceRequest? request = _picker.Pending;
        if (request == null)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        IsBusy = true;
        ShowLoading = true;
        ErrorMessage = null;

        try
        {
            _totalGroups = request.Groups.Count;

            Action<AudienceRow> onToggle = ToggleRow;
            List<AudienceRow> groupRows = new List<AudienceRow>();
            _activeGroups = new Dictionary<string, AudienceGroup>();
            for (int i = 0; i < request.Groups.Count; i++)
            {
                AudienceGroup group = request.Groups[i];
                _activeGroups[group.Id] = group;
                List<string> ids = new List<string>();
                ids.Add(group.Id);
                groupRows.Add(AudienceRow.Create(group.Id, group.Name, group.Number, true, CommunityText.NumberText(group.Number), ids, onToggle));
            }

            groupRows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _groupRows = groupRows;

            OperationResult<CommunityDirectory> result = await _communityService.GetDirectoryAsync();
            if (result.IsSuccess && result.Value != null)
            {
                _directory = result.Value;
                _supers = new List<SuperCommunity>(_directory.SuperCommunities);
                _supers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                for (int i = 0; i < _supers.Count; i++)
                {
                    SuperChoices.Add(_supers[i].Name + " (" + _supers[i].Number + ")");
                }
            }
            else
            {
                ErrorMessage = "AB Switches could not be loaded, so only single DTCs can be chosen right now.";
            }

            _selected = new HashSet<string>(request.Selected);

            // Open on the tab the admin tapped. Not loaded yet, so this does not clear anything.
            Segment = request.Segment == 1 ? 1 : 0;

            // Communities tab opened again: show the super community and communities that were chosen before.
            AudienceHierarchyState? before = request.Hierarchy;
            if (Segment == 1 && before != null && _directory != null)
            {
                int index = _supers.FindIndex(x => x.Id == before.SuperCommunityId);
                if (index >= 0)
                {
                    _communityIds = new HashSet<string>(before.CommunityIds);
                    _suppressSuperChange = true;
                    SelectedSuperIndex = index;
                    _suppressSuperChange = false;
                    BuildCommunityOptions();
                }
            }

            RebuildHierarchyGroups();

            _isLoaded = true;
            RefreshStates();
            Rebuild();
            UpdateSummary();
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
    }

    // ---------- Groups tab ----------

    private void ToggleRow(AudienceRow row)
    {
        if (row.State == 2)
        {
            for (int i = 0; i < row.GroupIds.Count; i++)
            {
                _selected.Remove(row.GroupIds[i]);
            }
        }
        else
        {
            for (int i = 0; i < row.GroupIds.Count; i++)
            {
                _selected.Add(row.GroupIds[i]);
            }
        }

        RefreshStates();
    }

    [RelayCommand]
    private void SelectAllResults()
    {
        for (int i = 0; i < Rows.Count; i++)
        {
            for (int j = 0; j < Rows[i].GroupIds.Count; j++)
            {
                _selected.Add(Rows[i].GroupIds[j]);
            }
        }

        RefreshStates();
    }

    // Clears the tab you are on.
    [RelayCommand]
    private void Clear()
    {
        if (Segment == 1)
        {
            _communityIds.Clear();
            for (int i = 0; i < CommunityOptions.Count; i++)
            {
                CommunityOptions[i].IsOn = false;
            }

            RebuildHierarchyGroups();
            return;
        }

        _selected.Clear();
        RefreshStates();
    }

    [RelayCommand]
    private void SelectSegment(string? index)
    {
        int value;
        if (int.TryParse(index, out value) && value >= 0 && value <= 1)
        {
            Segment = value;
        }
    }

    private void RefreshStates()
    {
        for (int i = 0; i < _groupRows.Count; i++)
        {
            _groupRows[i].Refresh(_selected);
        }

        UpdateSummary();
    }

    private void Rebuild()
    {
        if (!_isLoaded)
        {
            return;
        }

        string term = (SearchText ?? string.Empty).Trim();

        Rows.Clear();
        for (int i = 0; i < _groupRows.Count; i++)
        {
            AudienceRow row = _groupRows[i];
            if (term.Length > 0
                && row.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0
                && row.Number.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Rows.Add(row);
        }

        HasNoRows = Rows.Count == 0;
        if (!HasNoRows)
        {
            return;
        }

        if (term.Length > 0)
        {
            EmptyTitle = "No matches";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
        else if (_groupRows.Count == 0)
        {
            EmptyTitle = "No active DTCs";
            EmptyHint = "Create or activate a DTC first.";
        }
        else
        {
            EmptyTitle = "Nothing to show";
            EmptyHint = string.Empty;
        }
    }

    // ---------- Communities tab ----------

    // The communities of the chosen super community (and only those), A to Z.
    private void BuildCommunityOptions()
    {
        CommunityOptions.Clear();
        AllCommunitiesOn = false;

        if (_directory == null || !HasSuperSelected)
        {
            HasNoCommunities = false;
            UpdateCommunitiesButton();
            return;
        }

        List<Community> communities = new List<Community>(_directory.CommunitiesOfSuper(_supers[SelectedSuperIndex].Id));
        communities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < communities.Count; i++)
        {
            Community community = communities[i];
            int count = ActiveGroupIdsOf(community).Count;
            string detail = CommunityText.NumberText(community.Number) + " - " + CommunityText.Count(count, "DTC", "DTCs");
            CommunityOptions.Add(CommunityOptionRow.Create(community.Id, community.Name, detail, _communityIds.Contains(community.Id), ToggleCommunity));
        }

        HasNoCommunities = CommunityOptions.Count == 0;
        UpdateCommunitiesButton();
    }

    private List<string> ActiveGroupIdsOf(Community community)
    {
        List<string> ids = new List<string>();
        for (int i = 0; i < community.GroupIds.Count; i++)
        {
            string id = community.GroupIds[i];
            if (_activeGroups.ContainsKey(id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private void ToggleCommunity(CommunityOptionRow row)
    {
        if (row.IsOn)
        {
            _communityIds.Remove(row.Id);
            row.IsOn = false;
        }
        else
        {
            _communityIds.Add(row.Id);
            row.IsOn = true;
        }

        RebuildHierarchyGroups();
    }

    [RelayCommand]
    private void SelectAllCommunities()
    {
        bool turnOn = !AllCommunitiesOn;
        for (int i = 0; i < CommunityOptions.Count; i++)
        {
            CommunityOptions[i].IsOn = turnOn;
            if (turnOn)
            {
                _communityIds.Add(CommunityOptions[i].Id);
            }
            else
            {
                _communityIds.Remove(CommunityOptions[i].Id);
            }
        }

        RebuildHierarchyGroups();
    }

    [RelayCommand]
    private void ToggleCommunityPanel()
    {
        if (HasSuperSelected)
        {
            IsCommunityPanelOpen = !IsCommunityPanelOpen;
        }
    }

    // The groups of all ticked communities, each group once, read-only.
    private void RebuildHierarchyGroups()
    {
        HashSet<string> ids = new HashSet<string>();
        int ticked = 0;
        for (int i = 0; i < CommunityOptions.Count; i++)
        {
            if (!CommunityOptions[i].IsOn || _directory == null)
            {
                continue;
            }

            ticked++;
            Community? community = _directory.FindCommunity(CommunityOptions[i].Id);
            if (community == null)
            {
                continue;
            }

            List<string> groupIds = ActiveGroupIdsOf(community);
            for (int j = 0; j < groupIds.Count; j++)
            {
                ids.Add(groupIds[j]);
            }
        }

        _hierarchyGroupIds = ids;

        List<AudienceGroup> groups = new List<AudienceGroup>();
        foreach (string id in ids)
        {
            groups.Add(_activeGroups[id]);
        }

        groups.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        HierarchyGroups.Clear();
        for (int i = 0; i < groups.Count; i++)
        {
            HierarchyGroups.Add(HierarchyGroupRow.Create(groups[i].Name, CommunityText.NumberText(groups[i].Number)));
        }

        AllCommunitiesOn = CommunityOptions.Count > 0 && ticked == CommunityOptions.Count;
        HierarchyHeading = "DTCs that will get the message (" + groups.Count + ")";

        HasNoHierarchyGroups = groups.Count == 0;
        if (!HasSuperSelected)
        {
            HierarchyEmptyTitle = "Choose a Feeder";
            HierarchyEmptyHint = "Pick a Feeder, then tick the AB Switches that should get the message.";
        }
        else if (HasNoCommunities)
        {
            HierarchyEmptyTitle = "No AB Switches here yet";
            HierarchyEmptyHint = "This Feeder has no AB Switches. Add AB Switches to it first.";
        }
        else if (ticked == 0)
        {
            HierarchyEmptyTitle = "No AB Switch ticked";
            HierarchyEmptyHint = "Open the AB Switches list above and tick the ones that should get the message.";
        }
        else
        {
            HierarchyEmptyTitle = "No active DTCs";
            HierarchyEmptyHint = "The ticked AB Switches have no active DTCs.";
        }

        UpdateCommunitiesButton();
        UpdateSummary();
    }

    private void UpdateCommunitiesButton()
    {
        if (!HasSuperSelected)
        {
            CommunitiesButtonText = "Choose a Feeder first";
            return;
        }

        if (HasNoCommunities)
        {
            CommunitiesButtonText = "No AB Switches in this Feeder";
            return;
        }

        int ticked = 0;
        for (int i = 0; i < CommunityOptions.Count; i++)
        {
            if (CommunityOptions[i].IsOn)
            {
                ticked++;
            }
        }

        CommunitiesButtonText = ticked == 0
            ? "Choose AB Switches"
            : CommunityText.Count(ticked, "AB Switch", "AB Switches") + " selected";
    }

    // ---------- shared ----------

    private void UpdateSummary()
    {
        int count;
        if (Segment == 1)
        {
            count = _hierarchyGroupIds.Count;
            SummaryText = CommunityText.Count(count, "DTC", "DTCs") + " from " + CommunityText.Count(_communityIds.Count, "AB Switch", "AB Switches");
        }
        else
        {
            count = _selected.Count;
            SummaryText = count + " of " + CommunityText.Count(_totalGroups, "DTC", "DTCs") + " selected";
        }

        DoneText = count == 0 ? "Done" : "Done  -  " + CommunityText.Count(count, "DTC", "DTCs");
    }

    [RelayCommand]
    private async Task DoneAsync()
    {
        // Only the tab you are on counts. At least one group is needed to go on.
        int chosenCount = Segment == 1 ? _hierarchyGroupIds.Count : _selected.Count;
        if (chosenCount == 0)
        {
            ErrorMessage = "Choose at least one DTC.";
            return;
        }

        ErrorMessage = null;
        _isFinished = true;

        HashSet<string> result;
        AudienceHierarchyState? state = null;
        if (Segment == 1)
        {
            result = new HashSet<string>(_hierarchyGroupIds);
            if (result.Count > 0 && HasSuperSelected)
            {
                state = new AudienceHierarchyState();
                state.SuperCommunityId = _supers[SelectedSuperIndex].Id;
                state.CommunityIds = new HashSet<string>(_communityIds);
                state.GroupIds = new HashSet<string>(result);
            }
        }
        else
        {
            result = new HashSet<string>(_selected);
        }

        _picker.RememberHierarchy(state);

        // Close this page first, then hand over the choice: the next page opens only after this one is gone.
        await _navigation.GoToAsync("..");
        _picker.Complete(result);
    }

    public void OnClosed()
    {
        if (!_isFinished)
        {
            _picker.Complete(null);
        }
    }
}
