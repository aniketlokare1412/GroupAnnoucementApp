using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// One page for four jobs: new / edit community and new / edit super community.
// Route: communityedit?kind=community|super[&communityId=...]  (communityId holds the id of
// whichever kind is being edited; empty = create).
[QueryProperty(nameof(Kind), "kind")]
[QueryProperty(nameof(CommunityId), "communityId")]
public partial class CommunityEditViewModel : BaseViewModel
{
    public const string KindCommunity = "community";
    public const string KindSuper = "super";

    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly ICommunityAdminService _communityService;
    private readonly IGroupAdminService _groupService;

    private bool _isLoaded;
    private List<SuperCommunity> _supers = new List<SuperCommunity>();

    [ObservableProperty]
    private string? _kind;

    [ObservableProperty]
    private string? _communityId;

    [ObservableProperty]
    private string _titleText = "New community";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _number = string.Empty;

    // True for a community (shows the super community picker), false for a super community.
    [ObservableProperty]
    private bool _isCommunity = true;

    [ObservableProperty]
    private string _linkHeading = string.Empty;

    [ObservableProperty]
    private string _linkHint = string.Empty;

    [ObservableProperty]
    private bool _hasNoOptions;

    [ObservableProperty]
    private string _noOptionsText = string.Empty;

    // Index into SuperChoices. 0 = "None".
    [ObservableProperty]
    private int _selectedSuperIndex;

    // The groups (when editing a community) or communities (when editing a super community).
    public ObservableCollection<SelectableOptionItem> Options { get; } = new ObservableCollection<SelectableOptionItem>();

    public ObservableCollection<string> SuperChoices { get; } = new ObservableCollection<string>();

    public int MaxNumberLength
    {
        get { return CommunityLimits.MaxNumberLength; }
    }

    public int MaxNameLength
    {
        get { return CommunityLimits.MaxNameLength; }
    }

    public int MaxDescriptionLength
    {
        get { return CommunityLimits.MaxDescriptionLength; }
    }

    private bool IsEditing
    {
        get { return !string.IsNullOrEmpty(CommunityId); }
    }

    public CommunityEditViewModel(
        INavigationService navigation,
        ISessionService session,
        ICommunityAdminService communityService,
        IGroupAdminService groupService)
    {
        _navigation = navigation;
        _session = session;
        _communityService = communityService;
        _groupService = groupService;
    }

    // Called by the page every time it appears. Loads once.
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

        IsCommunity = !string.Equals(Kind, KindSuper, StringComparison.OrdinalIgnoreCase);
        string noun = IsCommunity ? "community" : "super community";
        TitleText = (IsEditing ? "Edit " : "New ") + noun;

        if (IsCommunity)
        {
            LinkHeading = "Groups in this community";
            LinkHint = "Tap to add or remove groups. A group can be in several communities.";
            NoOptionsText = "No groups yet. Create groups first, then add them here.";
        }
        else
        {
            LinkHeading = "Communities in this super community";
            LinkHint = "Tap to add or remove communities. A community can sit under only one super community, so picking one that is already elsewhere moves it here.";
            NoOptionsText = "No communities yet. Create communities first, then add them here.";
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            bool ok = IsCommunity ? await LoadCommunityAsync() : await LoadSuperAsync();
            if (ok)
            {
                _isLoaded = true;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadCommunityAsync()
    {
        OperationResult<List<AnnouncementGroup>> groupsResult = await _groupService.GetAllGroupsAsync();
        if (!groupsResult.IsSuccess)
        {
            ErrorMessage = groupsResult.ErrorMessage;
            return false;
        }

        OperationResult<List<SuperCommunity>> supersResult = await _communityService.GetAllSuperCommunitiesAsync();
        if (!supersResult.IsSuccess)
        {
            ErrorMessage = supersResult.ErrorMessage;
            return false;
        }

        Community? community = null;
        if (IsEditing)
        {
            OperationResult<Community?> result = await _communityService.GetCommunityAsync(CommunityId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the community.";
                return false;
            }

            community = result.Value;
            Name = community.Name;
            Description = community.Description;
            Number = community.Number;
        }

        _supers = supersResult.Value ?? new List<SuperCommunity>();
        SuperChoices.Clear();
        SuperChoices.Add("None");
        int selectedIndex = 0;
        for (int i = 0; i < _supers.Count; i++)
        {
            SuperChoices.Add(_supers[i].Name + " (" + _supers[i].Number + ")");
            if (community != null && community.SuperCommunityId == _supers[i].Id)
            {
                selectedIndex = i + 1;
            }
        }

        HashSet<string> chosen = new HashSet<string>(community != null ? community.GroupIds : new List<string>());
        Options.Clear();
        List<AnnouncementGroup> groups = groupsResult.Value ?? new List<AnnouncementGroup>();
        for (int i = 0; i < groups.Count; i++)
        {
            AnnouncementGroup group = groups[i];
            Options.Add(SelectableOptionItem.Create(group.Id, group.Name, group.Number, group.IsActive ? string.Empty : "inactive", chosen.Contains(group.Id)));
        }

        SelectedSuperIndex = selectedIndex;
        HasNoOptions = Options.Count == 0;
        return true;
    }

    private async Task<bool> LoadSuperAsync()
    {
        OperationResult<List<Community>> communitiesResult = await _communityService.GetAllCommunitiesAsync();
        if (!communitiesResult.IsSuccess)
        {
            ErrorMessage = communitiesResult.ErrorMessage;
            return false;
        }

        OperationResult<List<SuperCommunity>> supersResult = await _communityService.GetAllSuperCommunitiesAsync();
        if (!supersResult.IsSuccess)
        {
            ErrorMessage = supersResult.ErrorMessage;
            return false;
        }

        if (IsEditing)
        {
            OperationResult<SuperCommunity?> result = await _communityService.GetSuperCommunityAsync(CommunityId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the super community.";
                return false;
            }

            Name = result.Value.Name;
            Description = result.Value.Description;
            Number = result.Value.Number;
        }

        Dictionary<string, string> superNames = new Dictionary<string, string>();
        List<SuperCommunity> supers = supersResult.Value ?? new List<SuperCommunity>();
        for (int i = 0; i < supers.Count; i++)
        {
            superNames[supers[i].Id] = supers[i].Name;
        }

        Options.Clear();
        List<Community> communities = communitiesResult.Value ?? new List<Community>();
        for (int i = 0; i < communities.Count; i++)
        {
            Community community = communities[i];
            bool isHere = IsEditing && community.SuperCommunityId == CommunityId;

            string hint = string.Empty;
            if (!isHere && !string.IsNullOrEmpty(community.SuperCommunityId))
            {
                string otherName;
                hint = superNames.TryGetValue(community.SuperCommunityId, out otherName!) ? "in " + otherName : "in another super community";
            }

            Options.Add(SelectableOptionItem.Create(community.Id, community.Name, community.Number, hint, isHere));
        }

        HasNoOptions = Options.Count == 0;
        return true;
    }

    // Does nothing until the data has loaded, so a failed load can never be saved over.
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !_isLoaded)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            List<string> selectedIds = new List<string>();
            for (int i = 0; i < Options.Count; i++)
            {
                if (Options[i].IsSelected)
                {
                    selectedIds.Add(Options[i].Id);
                }
            }

            OperationResult result;
            if (IsCommunity)
            {
                string superId = string.Empty;
                if (SelectedSuperIndex > 0 && SelectedSuperIndex <= _supers.Count)
                {
                    superId = _supers[SelectedSuperIndex - 1].Id;
                }

                if (IsEditing)
                {
                    result = await _communityService.UpdateCommunityAsync(CommunityId!, Name, Description, Number, selectedIds, superId);
                }
                else
                {
                    result = await _communityService.CreateCommunityAsync(Name, Description, Number, selectedIds, superId);
                }
            }
            else
            {
                if (IsEditing)
                {
                    result = await _communityService.UpdateSuperCommunityAsync(CommunityId!, Name, Description, Number, selectedIds);
                }
                else
                {
                    result = await _communityService.CreateSuperCommunityAsync(Name, Description, Number, selectedIds);
                }
            }

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            await _navigation.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
