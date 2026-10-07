using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: the members of one group (groupId comes from the route). Tap a member to remove them.
[QueryProperty(nameof(GroupId), "groupId")]
public partial class GroupMembersViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly IMembershipAdminService _membershipService;
    private readonly IDialogService _dialogs;

    private bool _hasLoaded;

    public ObservableCollection<MemberListItem> Members { get; } = new ObservableCollection<MemberListItem>();

    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private MemberListItem? _selectedMember;

    [ObservableProperty]
    private bool _hasNoMembers;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public GroupMembersViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        IMembershipAdminService membershipService,
        IDialogService dialogs)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _membershipService = membershipService;
        _dialogs = dialogs;
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedMemberChanged(MemberListItem? value)
    {
        if (value != null)
        {
            _ = RemoveMemberAsync(value);
        }
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
                Members.Add(MemberListItem.FromEntry(entries[i]));
            }

            _hasLoaded = true;
            HasNoMembers = Members.Count == 0;
            SummaryText = groupResult.Value.Name + "  |  " + Members.Count + " members";
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
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REMOVE MEMBER ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedMember = null;
        }
    }
}
