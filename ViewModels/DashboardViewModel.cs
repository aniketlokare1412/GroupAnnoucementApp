using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _roleText = string.Empty;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private bool _isRegularUser;

    public DashboardViewModel(INavigationService navigation, ISessionService session)
    {
        _navigation = navigation;
        _session = session;
    }

    // Called by the page every time it appears (Shell reuses the page instance,
    // so the constructor alone would show stale data after a user switch).
    public async Task RefreshAsync()
    {
        ErrorMessage = null;

        UserProfile? profile = _session.CurrentProfile;
        if (profile == null)
        {
            // Safety net: never show the dashboard without a loaded profile.
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        DisplayName = profile.Name;
        UserEmail = profile.Email;
        IsAdmin = _session.IsAdmin;
        IsRegularUser = !IsAdmin;

        if (IsAdmin)
        {
            RoleText = "Role: Administrator";
        }
        else
        {
            RoleText = "Role: Member";
        }
    }

    [RelayCommand]
    private Task OpenUsersAsync()
    {
        return _navigation.GoToAsync(Routes.Users);
    }

    [RelayCommand]
    private Task OpenGroupsAsync()
    {
        return _navigation.GoToAsync(Routes.Groups);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            OperationResult result = await _session.SignOutAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            await _navigation.GoToAsync(Routes.Login);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
