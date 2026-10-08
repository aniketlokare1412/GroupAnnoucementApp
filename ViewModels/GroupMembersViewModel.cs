using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: the members of one group (groupId comes from the route). The x on a card removes that member.
[QueryProperty(nameof(GroupId), "groupId")]
public partial class GroupMembersViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly IMembershipAdminService _membershipService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private bool _hasLoaded;

    public ObservableCollection<MemberListItem> Members { get; } = new ObservableCollection<MemberListItem>();

    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private bool _hasNoMembers;

    // Header: the group's name, its initials and "12 members".
    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private string _countText = string.Empty;

    // True only for the very first load, so a refresh or a removal never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    public GroupMembersViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        IMembershipAdminService membershipService,
        IDialogService dialogs,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _membershipService = membershipService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Called by the page every time it appears (so the list is fresh after adding members).
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrEmpty(GroupId))
        {
            ErrorMessage = "Unknown group.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            OperationResult<AnnouncementGroup?> groupResult = await _groupService.GetGroupAsync(GroupId);
            if (!groupResult.IsSuccess || groupResult.Value == null)
            {
                ErrorMessage = groupResult.ErrorMessage ?? "We could not load the group.";
                return;
            }

            OperationResult<List<GroupMemberEntry>> result = await _membershipService.GetMembersAsync(GroupId);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            Members.Clear();
            List<GroupMemberEntry> entries = result.Value ?? new List<GroupMemberEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                Members.Add(MemberListItem.FromEntry(entries[i], RemoveMemberAsync));
            }

            _hasLoaded = true;
            HasNoMembers = Members.Count == 0;
            GroupName = groupResult.Value.Name;
            Initials = InitialsHelper.From(groupResult.Value.Name);
            CountText = Members.Count == 1 ? "1 member" : Members.Count + " members";
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

    [RelayCommand]
    private Task AddMembersAsync()
    {
        if (string.IsNullOrEmpty(GroupId) || !_hasLoaded)
        {
            return Task.CompletedTask;
        }

        string route = Routes.AddMembers + "?groupId=" + Uri.EscapeDataString(GroupId);
        return _navigation.GoToAsync(route);
    }

    private async Task RemoveMemberAsync(MemberListItem item)
    {
        try
        {
            if (IsBusy || string.IsNullOrEmpty(GroupId))
            {
                return;
            }

            bool confirmed = await _dialogs.ConfirmAsync(
                "Remove member",
                "Remove " + item.Name + " from this group? It will disappear from their My groups. They can join again later.",
                "Remove",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            bool removed;
            try
            {
                OperationResult result = await _membershipService.RemoveMemberAsync(GroupId, item.UserId);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.ErrorMessage;
                    removed = false;
                }
                else
                {
                    removed = true;
                }
            }
            finally
            {
                IsBusy = false;
            }

            if (removed)
            {
                _toasts.Show("Removed " + item.Name);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REMOVE MEMBER ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }
}
