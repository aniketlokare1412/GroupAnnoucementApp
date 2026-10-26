using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Used for both "New group" (no groupId) and "Edit group" (groupId passed in the route).
[QueryProperty(nameof(GroupId), "groupId")]
public partial class GroupEditViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly ICommunityAdminService _communityService;
    private readonly IToastService _toasts;

    private bool _isLoaded;

    // The structure loaded for the optional "Super community" and "Communities" pickers (new group only).
    private List<SuperCommunity> _supers = new List<SuperCommunity>();
    private List<Community> _communities = new List<Community>();

    // Set by Shell from the route (groupedit?groupId=...). Empty means "create a new group".
    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string _titleText = "New DTC";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _number = string.Empty;

    // ---- Optional placement of a NEW group: first a super community, then communities under it ----

    // Only a new group shows these pickers (an existing group is placed from the community screens).
    [ObservableProperty]
    private bool _showStructure;

    // The first drop-down. Index 0 is "None".
    [ObservableProperty]
    private List<string> _superNames = new List<string> { "None" };

    [ObservableProperty]
    private int _superIndex;

    // The second drop-down appears once a super community is chosen.
    [ObservableProperty]
    private bool _showCommunities;

    [ObservableProperty]
    private bool _isCommunityListOpen;

    [ObservableProperty]
    private bool _hasNoCommunityOptions;

    [ObservableProperty]
    private string _communitySummary = "Choose AB Switches (optional)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStructureNote))]
    private string? _structureNote;

    public bool HasStructureNote
    {
        get { return !string.IsNullOrEmpty(StructureNote); }
    }

    public ObservableCollection<AbSwitchPickItem> CommunityOptions { get; } = new ObservableCollection<AbSwitchPickItem>();

    public int MaxNumberLength
    {
        get { return GroupLimits.MaxNumberLength; }
    }

    public int MaxNameLength
    {
        get { return GroupLimits.MaxNameLength; }
    }

    public int MaxDescriptionLength
    {
        get { return GroupLimits.MaxDescriptionLength; }
    }

    private bool IsEditing
    {
        get { return !string.IsNullOrEmpty(GroupId); }
    }

    public GroupEditViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        ICommunityAdminService communityService,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _communityService = communityService;
        _toasts = toasts;
    }

    // Called by the page every time it appears. Loads the group once when editing.
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

        if (!IsEditing)
        {
            TitleText = "New DTC";
            ShowStructure = true;
            _isLoaded = true;
            await LoadStructureAsync();
            return;
        }

        TitleText = "Edit DTC";
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<AnnouncementGroup?> result = await _groupService.GetGroupAsync(GroupId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the DTC.";
                return;
            }

            Name = result.Value.Name;
            Description = result.Value.Description;
            Number = result.Value.Number;
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // The super communities and communities for the two optional drop-downs. A failure here never blocks creating the group.
    private async Task LoadStructureAsync()
    {
        StructureNote = null;

        OperationResult<List<SuperCommunity>> supers = await _communityService.GetAllSuperCommunitiesAsync();
        OperationResult<List<Community>> communities = await _communityService.GetAllCommunitiesAsync();
        if (!supers.IsSuccess || !communities.IsSuccess || supers.Value == null || communities.Value == null)
        {
            StructureNote = "Feeders and AB Switches could not be loaded. You can still create the DTC without them.";
            return;
        }

        _supers = supers.Value;
        _communities = communities.Value;

        List<string> names = new List<string> { "None" };
        for (int i = 0; i < _supers.Count; i++)
        {
            names.Add(_supers[i].Name);
        }

        SuperNames = names;
        SuperIndex = 0;
    }

    // Choosing another super community clears the communities that were ticked under the old one.
    partial void OnSuperIndexChanged(int value)
    {
        foreach (AbSwitchPickItem old in CommunityOptions)
        {
            old.PropertyChanged -= OnCommunityOptionChanged;
        }

        CommunityOptions.Clear();
        IsCommunityListOpen = false;

        if (value <= 0 || value > _supers.Count)
        {
            ShowCommunities = false;
            HasNoCommunityOptions = false;
            UpdateCommunitySummary();
            return;
        }

        string superId = _supers[value - 1].Id;
        for (int i = 0; i < _communities.Count; i++)
        {
            if (_communities[i].SuperCommunityId == superId)
            {
                AbSwitchPickItem item = AbSwitchPickItem.FromCommunity(_communities[i]);
                item.PropertyChanged += OnCommunityOptionChanged;
                CommunityOptions.Add(item);
            }
        }

        ShowCommunities = true;
        HasNoCommunityOptions = CommunityOptions.Count == 0;
        UpdateCommunitySummary();
    }

    private void OnCommunityOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AbSwitchPickItem.IsOn))
        {
            UpdateCommunitySummary();
        }
    }

    private void UpdateCommunitySummary()
    {
        int ticked = 0;
        foreach (AbSwitchPickItem item in CommunityOptions)
        {
            if (item.IsOn)
            {
                ticked++;
            }
        }

        CommunitySummary = ticked == 0
            ? "Choose AB Switches (optional)"
            : CommunityText.Count(ticked, "AB Switch", "AB Switches") + " selected";
    }

    [RelayCommand]
    private void ToggleCommunityList()
    {
        if (CommunityOptions.Count == 0)
        {
            return;
        }

        IsCommunityListOpen = !IsCommunityListOpen;
    }

    // Does nothing until the group has loaded, so a failed load can never be saved over.
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
            if (IsEditing)
            {
                OperationResult updated = await _groupService.UpdateGroupAsync(GroupId!, Name, Description, Number);
                if (!updated.IsSuccess)
                {
                    ErrorMessage = updated.ErrorMessage;
                    return;
                }
            }
            else
            {
                List<string> communityIds = new List<string>();
                foreach (AbSwitchPickItem item in CommunityOptions)
                {
                    if (item.IsOn)
                    {
                        communityIds.Add(item.Id);
                    }
                }

                OperationResult<string?> created = await _groupService.CreateGroupAsync(Name, Description, Number, communityIds);
                if (!created.IsSuccess)
                {
                    ErrorMessage = created.ErrorMessage;
                    return;
                }

                // The group exists. If some communities could not be updated, say so (the group is not lost).
                if (!string.IsNullOrEmpty(created.Value))
                {
                    _toasts.Show(created.Value);
                }
            }

            await _navigation.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigation.GoToAsync("..");
    }
}
