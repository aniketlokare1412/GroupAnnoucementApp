using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin Groups tab. Every group is a card with icon buttons (announcements, members, edit,
// activate / deactivate). The old "tap a row, pick from a list" dialog is gone.
public partial class GroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private List<AnnouncementGroup> _allGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    public ObservableCollection<GroupListItem> Groups { get; } = new ObservableCollection<GroupListItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasNoGroups;

    // "5 groups  -  3 of 10 active", or "2 of 5 groups" while searching.
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

    public GroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        IDialogService dialogs,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears (so the list is fresh after adding or editing).
    public async Task LoadAsync()
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

    // The one main action on this screen.
    [RelayCommand]
    private Task AddGroupAsync()
    {
        return _navigation.GoToAsync(Routes.GroupEdit);
    }

    // Admin: write one announcement and post it to one or more groups.
    [RelayCommand]
    private Task NewAnnouncementAsync()
    {
        return _navigation.GoToAsync(Routes.AnnouncementEdit);
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
                message = "Activate \"" + item.Name + "\"? It will count towards the limit of " + GroupLimits.MaxActiveGroups + " active groups.";
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
        string term = (SearchText ?? string.Empty).Trim();

        Groups.Clear();
        int activeCount = 0;
        for (int i = 0; i < _allGroups.Count; i++)
        {
            AnnouncementGroup group = _allGroups[i];

            if (group.IsActive)
            {
                activeCount++;
            }

            if (Matches(group, term))
            {
                Groups.Add(GroupListItem.FromGroup(group, OpenAnnouncementsAsync, OpenMembersAsync, EditAsync, ChangeActiveAsync));
            }
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
            SummaryText = total + "  -  " + activeCount + " of " + GroupLimits.MaxActiveGroups + " active";
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
