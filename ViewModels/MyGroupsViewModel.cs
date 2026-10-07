using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Regular user: the active groups I am a member of. Tap a group to leave it.
// (Feature 7 will make a tap open the group's announcements instead.)
public partial class MyGroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IMembershipService _membershipService;
    private readonly IDialogService _dialogs;

    private bool _hasLoaded;

    public ObservableCollection<GroupListItem> Groups { get; } = new ObservableCollection<GroupListItem>();

    [ObservableProperty]
    private GroupListItem? _selectedGroup;

    [ObservableProperty]
    private bool _hasNoGroups;

    public MyGroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IMembershipService membershipService,
        IDialogService dialogs)
    {
        _navigation = navigation;
        _session = session;
        _membershipService = membershipService;
        _dialogs = dialogs;
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedGroupChanged(GroupListItem? value)
    {
        if (value != null)
        {
            _ = LeaveGroupAsync(value);
        }
    }

    // Called by the page every time it appears (so the list is fresh after joining a group).
    public async Task LoadAsync()
    {
        if (_session.CurrentProfile == null || _session.IsAdmin)
        {
            // Admins manage groups elsewhere; never show this page to them.
            await _navigation.GoToAsync("..");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<List<AnnouncementGroup>> result = await _membershipService.GetMyGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            Groups.Clear();
            List<AnnouncementGroup> groups = result.Value ?? new List<AnnouncementGroup>();
            for (int i = 0; i < groups.Count; i++)
            {
                Groups.Add(GroupListItem.FromGroup(groups[i]));
            }

            _hasLoaded = true;
            HasNoGroups = Groups.Count == 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    [RelayCommand]
    private Task JoinGroupAsync()
    {
        return _navigation.GoToAsync(Routes.JoinGroups);
    }

    private async Task LeaveGroupAsync(GroupListItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            bool confirmed = await _dialogs.ConfirmAsync(
                "Leave group",
                "Leave \"" + item.Name + "\"? It will disappear from your groups. You can join again later.",
                "Leave",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            bool left;
            try
            {
                OperationResult result = await _membershipService.LeaveGroupAsync(item.Id);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.ErrorMessage;
                    left = false;
                }
                else
                {
                    left = true;
                }
            }
            finally
            {
                IsBusy = false;
            }

            if (left)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LEAVE GROUP ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedGroup = null;
        }
    }
}
