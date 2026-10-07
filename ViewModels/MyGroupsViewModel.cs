using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Regular user: the active groups I am a member of.
// Tap a group (or its Announcements button) to read its announcements. The Leave button leaves it.
public partial class MyGroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IMembershipService _membershipService;
    private readonly IDialogService _dialogs;

    public ObservableCollection<MyGroupListItem> Groups { get; } = new ObservableCollection<MyGroupListItem>();

    [ObservableProperty]
    private MyGroupListItem? _selectedGroup;

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

    // Runs automatically when a row (not one of its buttons) is tapped.
    partial void OnSelectedGroupChanged(MyGroupListItem? value)
    {
        if (value != null)
        {
            _ = OpenFromSelectionAsync(value);
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
                Groups.Add(MyGroupListItem.FromGroup(groups[i], OpenAnnouncementsAsync, LeaveGroupAsync));
            }

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

    private async Task OpenFromSelectionAsync(MyGroupListItem item)
    {
        try
        {
            await OpenAnnouncementsAsync(item);
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedGroup = null;
        }
    }

    private async Task OpenAnnouncementsAsync(MyGroupListItem item)
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

    private async Task LeaveGroupAsync(MyGroupListItem item)
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
    }
}
