using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class UsersViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IUserAdminService _adminService;
    private readonly IDialogService _dialogs;

    private List<UserProfile> _allUsers = new List<UserProfile>();
    private bool _hasLoaded;

    public ObservableCollection<UserListItem> Users { get; } = new ObservableCollection<UserListItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private UserListItem? _selectedUser;

    [ObservableProperty]
    private bool _hasNoUsers;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public UsersViewModel(
        INavigationService navigation,
        ISessionService session,
        IAuthService authService,
        IUserAdminService adminService,
        IDialogService dialogs)
    {
        _navigation = navigation;
        _session = session;
        _authService = authService;
        _adminService = adminService;
        _dialogs = dialogs;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedUserChanged(UserListItem? value)
    {
        if (value != null)
        {
            _ = ManageUserAsync(value);
        }
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
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    private async Task ManageUserAsync(UserListItem item)
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

            string activeOption = item.IsActive ? "Deactivate user" : "Activate user";
            string roleOption = item.IsAdmin ? "Remove admin rights" : "Make admin";

            string? choice = await _dialogs.ChooseAsync(item.Name, "Cancel", new string[] { activeOption, roleOption });
            if (choice == null)
            {
                return;
            }

            if (choice == activeOption)
            {
                await ChangeActiveAsync(item);
            }
            else if (choice == roleOption)
            {
                await ChangeRoleAsync(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MANAGE USER ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedUser = null;
        }
    }

    private async Task ChangeActiveAsync(UserListItem item)
    {
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
            await LoadAsync();
        }
    }

    private async Task ChangeRoleAsync(UserListItem item)
    {
        string newType;
        string title;
        string message;
        string accept;
        if (item.IsAdmin)
        {
            newType = UserTypes.Regular;
            title = "Remove admin rights";
            message = "Remove admin rights from " + item.Name + "? They will become a regular member.";
            accept = "Remove";
        }
        else
        {
            newType = UserTypes.Admin;
            title = "Make admin";
            message = "Make " + item.Name + " an administrator? Administrators can manage users, groups and announcements.";
            accept = "Make admin";
        }

        bool confirmed = await _dialogs.ConfirmAsync(title, message, accept, "Cancel");
        if (!confirmed)
        {
            return;
        }

        bool saved = await RunChangeAsync(_adminService.SetUserTypeAsync(item.Id, newType));
        if (saved)
        {
            await LoadAsync();
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
        for (int i = 0; i < _allUsers.Count; i++)
        {
            UserProfile profile = _allUsers[i];
            if (Matches(profile, term))
            {
                Users.Add(UserListItem.FromProfile(profile, currentUid));
            }
        }

        HasNoUsers = _hasLoaded && Users.Count == 0;

        if (_hasLoaded)
        {
            SummaryText = Users.Count + " of " + _allUsers.Count + " users";
        }
        else
        {
            SummaryText = string.Empty;
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
