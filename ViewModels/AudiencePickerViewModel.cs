using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Full-screen picker for who an announcement or outage goes to. Built for a few hundred groups:
//   * search box (name or number), three tabs: Groups | Communities | Super communities
//   * tick a community or super community to tick all of its active groups
//   * "Select all results" ticks everything the search shows, "Selected only" shows what is ticked
//   * Done hands the ticked group ids back; going back keeps the old selection.
// Whatever is ticked is always a set of group ids, so a group ticked through a community and
// through the Groups tab is the same tick.
public partial class AudiencePickerViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAudiencePickerService _picker;
    private readonly ICommunityService _communityService;

    private List<AudienceRow> _groupRows = new List<AudienceRow>();
    private List<AudienceRow> _communityRows = new List<AudienceRow>();
    private List<AudienceRow> _superRows = new List<AudienceRow>();
    private HashSet<string> _selected = new HashSet<string>();
    private int _totalGroups;
    private bool _isLoaded;
    private bool _isFinished;

    // The rows of the tab that is showing, after search and "selected only".
    public ObservableCollection<AudienceRow> Rows { get; } = new ObservableCollection<AudienceRow>();

    // 0 = Groups, 1 = Communities, 2 = Super communities.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGroupsSegment))]
    [NotifyPropertyChangedFor(nameof(IsNotGroupsSegment))]
    [NotifyPropertyChangedFor(nameof(IsCommunitiesSegment))]
    [NotifyPropertyChangedFor(nameof(IsNotCommunitiesSegment))]
    [NotifyPropertyChangedFor(nameof(IsSuperSegment))]
    [NotifyPropertyChangedFor(nameof(IsNotSuperSegment))]
    private int _segment;

    public bool IsGroupsSegment => Segment == 0;
    public bool IsNotGroupsSegment => Segment != 0;
    public bool IsCommunitiesSegment => Segment == 1;
    public bool IsNotCommunitiesSegment => Segment != 1;
    public bool IsSuperSegment => Segment == 2;
    public bool IsNotSuperSegment => Segment != 2;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedOnlyText))]
    private bool _selectedOnly;

    public string SelectedOnlyText
    {
        get { return SelectedOnly ? "Show all" : "Selected only"; }
    }

    // "12 of 250 groups selected".
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

    partial void OnSegmentChanged(int value)
    {
        Rebuild();
    }

    partial void OnSelectedOnlyChanged(bool value)
    {
        Rebuild();
    }

    // Called by the page every time it appears. Builds the rows once.
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
            // Nothing asked for this page (for example the app was restored on it): leave.
            await _navigation.GoToAsync("..");
            return;
        }

        IsBusy = true;
        ShowLoading = true;
        ErrorMessage = null;

        try
        {
            _selected = new HashSet<string>(request.Selected);
            _totalGroups = request.Groups.Count;

            Action<AudienceRow> onToggle = ToggleRow;
            List<AudienceRow> groupRows = new List<AudienceRow>();
            HashSet<string> activeIds = new HashSet<string>();
            for (int i = 0; i < request.Groups.Count; i++)
            {
                AudienceGroup group = request.Groups[i];
                activeIds.Add(group.Id);
                List<string> ids = new List<string>();
                ids.Add(group.Id);
                groupRows.Add(AudienceRow.Create(group.Id, group.Name, group.Number, true, CommunityText.NumberText(group.Number), ids, onToggle));
            }

            groupRows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _groupRows = groupRows;

            // Communities are optional: if they cannot be loaded the Groups tab still works.
            _communityRows = new List<AudienceRow>();
            _superRows = new List<AudienceRow>();
            OperationResult<CommunityDirectory> result = await _communityService.GetDirectoryAsync();
            if (result.IsSuccess && result.Value != null)
            {
                BuildCommunityRows(result.Value, activeIds, onToggle);
            }
            else
            {
                ErrorMessage = "Communities could not be loaded, so only single groups can be chosen right now.";
            }

            _isLoaded = true;
            RefreshStates();
            Rebuild();
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
    }

    private void BuildCommunityRows(CommunityDirectory directory, HashSet<string> activeIds, Action<AudienceRow> onToggle)
    {
        for (int i = 0; i < directory.Communities.Count; i++)
        {
            Community community = directory.Communities[i];
            List<string> ids = KeepActive(community.GroupIds, activeIds);
            if (ids.Count == 0)
            {
                continue;
            }

            _communityRows.Add(AudienceRow.Create(community.Id, community.Name, community.Number, false, CommunityText.Count(ids.Count, "group", "groups"), ids, onToggle));
        }

        for (int i = 0; i < directory.SuperCommunities.Count; i++)
        {
            SuperCommunity superCommunity = directory.SuperCommunities[i];
            List<string> ids = KeepActive(directory.GroupIdsOfSuper(superCommunity.Id), activeIds);
            if (ids.Count == 0)
            {
                continue;
            }

            _superRows.Add(AudienceRow.Create(superCommunity.Id, superCommunity.Name, superCommunity.Number, false, CommunityText.Count(ids.Count, "group", "groups"), ids, onToggle));
        }
    }

    // Only active groups are in the picker, and only those can receive something.
    private static List<string> KeepActive(List<string> ids, HashSet<string> activeIds)
    {
        List<string> kept = new List<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            if (activeIds.Contains(ids[i]) && !kept.Contains(ids[i]))
            {
                kept.Add(ids[i]);
            }
        }

        return kept;
    }

    // ---------- ticking ----------

    // A row that is fully ticked is unticked; anything else ticks all of its groups.
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

        AfterSelectionChanged();
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

        AfterSelectionChanged();
    }

    [RelayCommand]
    private void Clear()
    {
        _selected.Clear();
        AfterSelectionChanged();
    }

    [RelayCommand]
    private void ToggleSelectedOnly()
    {
        SelectedOnly = !SelectedOnly;
    }

    [RelayCommand]
    private void SelectSegment(string? index)
    {
        int value;
        if (int.TryParse(index, out value) && value >= 0 && value <= 2)
        {
            Segment = value;
        }
    }

    private void AfterSelectionChanged()
    {
        RefreshStates();

        // With "Selected only" a row that was just unticked must leave the list.
        if (SelectedOnly)
        {
            Rebuild();
        }
    }

    private void RefreshStates()
    {
        for (int i = 0; i < _groupRows.Count; i++)
        {
            _groupRows[i].Refresh(_selected);
        }

        for (int i = 0; i < _communityRows.Count; i++)
        {
            _communityRows[i].Refresh(_selected);
        }

        for (int i = 0; i < _superRows.Count; i++)
        {
            _superRows[i].Refresh(_selected);
        }

        int count = _selected.Count;
        SummaryText = count + " of " + CommunityText.Count(_totalGroups, "group", "groups") + " selected";
        DoneText = count == 0 ? "Done" : "Done  -  " + CommunityText.Count(count, "group", "groups");
    }

    // ---------- showing the rows ----------

    private void Rebuild()
    {
        if (!_isLoaded)
        {
            return;
        }

        List<AudienceRow> source = Segment == 1 ? _communityRows : Segment == 2 ? _superRows : _groupRows;
        string term = (SearchText ?? string.Empty).Trim();

        Rows.Clear();
        for (int i = 0; i < source.Count; i++)
        {
            AudienceRow row = source[i];
            if (SelectedOnly && row.State == 0)
            {
                continue;
            }

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

        string what = Segment == 1 ? "communities" : Segment == 2 ? "super communities" : "groups";
        if (SelectedOnly)
        {
            EmptyTitle = "Nothing selected here";
            EmptyHint = "Tap Show all to see every " + what + " again.";
        }
        else if (term.Length > 0)
        {
            EmptyTitle = "No matches";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
        else if (source.Count == 0)
        {
            EmptyTitle = Segment == 0 ? "No active groups" : "No " + what + " with active groups";
            EmptyHint = Segment == 0 ? "Create or activate a group first." : "Add active groups to a " + (Segment == 1 ? "community" : "super community") + " first.";
        }
        else
        {
            EmptyTitle = "Nothing to show";
            EmptyHint = string.Empty;
        }
    }

    // ---------- leaving ----------

    [RelayCommand]
    private async Task DoneAsync()
    {
        _isFinished = true;
        _picker.Complete(new HashSet<string>(_selected));
        await _navigation.GoToAsync("..");
    }

    // The page calls this when it disappears. Going back without Done keeps the old selection.
    public void OnClosed()
    {
        if (!_isFinished)
        {
            _picker.Complete(null);
        }
    }
}
