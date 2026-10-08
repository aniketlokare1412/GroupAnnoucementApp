using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin Users tab. Every user is a compact card with icon buttons (make / remove admin,
// activate / deactivate). The old "tap a row, pick from a list" dialog is gone.
public partial class UsersViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IUserAdminService _adminService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private List<UserProfile> _allUsers = new List<UserProfile>();
    private bool _hasLoaded;

    public ObservableCollection<UserListItem> Users { get; } = new ObservableCollection<UserListItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasNoUsers;

    // "12 users  -  2 admins", or "3 of 12 users" while searching.
    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _emptyTitle = string.Empty;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    // True only for the very first load, so a refresh or a change never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    public UsersViewModel(
        INavigationService navigation,
        ISessionService session,
        IAuthService authService,
        IUserAdminService adminService,
        IDialogService dialogs,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _authService = authService;
        _adminService = adminService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears.
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
            OperationResult<List<UserProfile>> result = await _adminService.GetAllUsersAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _allUsers = result.Value ?? new List<UserProfile>();
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

    private async Task ChangeActiveAsync(UserListItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            if (item.IsSelf)
            {
                await _dialogs.AlertAsync("Your account", "You cannot change your own role or status.", "OK");
                return;
            }

            bool newValue = !item.IsActive;

            string title;
            string message;
            string accept;
            if (newValue)
            {
                title = "Activate user";
                message = "Activate " + item.Name + "? They will be able to use the app again.";
                accept = "Activate";
            }
            else
            {
                title = "Deactivate user";
                message = "Deactivate " + item.Name + "? They will no longer be able to use the app.";
                accept = "Deactivate";
            }

            bool confirmed = await _dialogs.ConfirmAsync(title, message, accept, "Cancel");
            if (!confirmed)
            {
                return;
            }

            bool saved = await RunChangeAsync(_adminService.SetActiveAsync(item.Id, newValue));
            if (saved)
            {
                _toasts.Show(newValue ? "Activated " + item.Name : "Deactivated " + item.Name);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CHANGE USER ACTIVE ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private async Task ChangeRoleAsync(UserListItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            if (item.IsSelf)
            {
                await _dialogs.AlertAsync("Your account", "You cannot change your own role or status.", "OK");
                return;
            }

            string newType;
            string title;
            string message;
            string accept;
            string toast;
            if (item.IsAdmin)
            {
                newType = UserTypes.Regular;
                title = "Remove admin rights";
                message = "Remove admin rights from " + item.Name + "? They will become a regular member.";
                accept = "Remove";
                toast = item.Name + " is now a member";
            }
            else
            {
                newType = UserTypes.Admin;
                title = "Make admin";
                message = "Make " + item.Name + " an administrator? Administrators can manage users, groups and announcements.";
                accept = "Make admin";
                toast = item.Name + " is now an administrator";
            }

            bool confirmed = await _dialogs.ConfirmAsync(title, message, accept, "Cancel");
            if (!confirmed)
            {
                return;
            }

            bool saved = await RunChangeAsync(_adminService.SetUserTypeAsync(item.Id, newType));
            if (saved)
            {
                _toasts.Show(toast);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CHANGE USER ROLE ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    // Runs one admin change while showing the busy indicator. Returns true when it was saved.
    private async Task<bool> RunChangeAsync(Task<OperationResult> change)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult result = await change;
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return false;
            }

            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();
        string? currentUid = _authService.GetCurrentUserId();

        Users.Clear();
        int adminCount = 0;
        for (int i = 0; i < _allUsers.Count; i++)
        {
            UserProfile profile = _allUsers[i];

            if (string.Equals(profile.UserType, UserTypes.Admin, StringComparison.Ordinal))
            {
                adminCount++;
            }

            if (Matches(profile, term))
            {
                Users.Add(UserListItem.FromProfile(profile, currentUid, ChangeRoleAsync, ChangeActiveAsync));
            }
        }

        HasNoUsers = _hasLoaded && Users.Count == 0;

        if (!_hasLoaded)
        {
            SummaryText = string.Empty;
            return;
        }

        if (term.Length > 0)
        {
            SummaryText = Users.Count + " of " + _allUsers.Count + " users";
        }
        else
        {
            string total = _allUsers.Count == 1 ? "1 user" : _allUsers.Count + " users";
            string admins = adminCount == 1 ? "1 admin" : adminCount + " admins";
            SummaryText = total + "  -  " + admins;
        }

        if (_allUsers.Count == 0)
        {
            EmptyTitle = "No users yet";
            EmptyHint = "People appear here after they register.";
        }
        else
        {
            EmptyTitle = "No users found";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
    }

    private static bool Matches(UserProfile profile, string term)
    {
        if (term.Length == 0)
        {
            return true;
        }

        if (ContainsText(profile.Name, term))
        {
            return true;
        }

        if (ContainsText(profile.Email, term))
        {
            return true;
        }

        if (ContainsText(profile.Phone, term))
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
