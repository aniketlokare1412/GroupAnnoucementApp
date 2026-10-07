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

    private bool _isLoaded;

    // Set by Shell from the route (groupedit?groupId=...). Empty means "create a new group".
    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string _titleText = "New group";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

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
        IGroupAdminService groupService)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
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
            TitleText = "New group";
            _isLoaded = true;
            return;
        }

        TitleText = "Edit group";
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<AnnouncementGroup?> result = await _groupService.GetGroupAsync(GroupId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the group.";
                return;
            }

            Name = result.Value.Name;
            Description = result.Value.Description;
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
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
            OperationResult result;
            if (IsEditing)
            {
                result = await _groupService.UpdateGroupAsync(GroupId!, Name, Description);
            }
            else
            {
                result = await _groupService.CreateGroupAsync(Name, Description);
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

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigation.GoToAsync("..");
    }
}
