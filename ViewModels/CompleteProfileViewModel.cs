using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Shown when someone is signed in but has no users/{uid} profile
// (for example an account created in the Firebase console, or a registration that was interrupted).
public partial class CompleteProfileViewModel : BaseViewModel
{
    private const int MaxNameLength = 100;

    private readonly INavigationService _navigation;
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    public CompleteProfileViewModel(
        INavigationService navigation,
        IAuthService authService,
        IUserService userService,
        ISessionService session)
    {
        _navigation = navigation;
        _authService = authService;
        _userService = userService;
        _session = session;
    }

    public void Prepare()
    {
        Name = string.Empty;
        Phone = string.Empty;
        ErrorMessage = null;
        UserEmail = _authService.GetCurrentUserEmail() ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanName = (Name ?? string.Empty).Trim();
        string cleanPhone = (Phone ?? string.Empty).Trim();

        string? validationError = Validate(cleanName, cleanPhone);
        if (validationError != null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _userService.CreateOwnProfileAsync(cleanName, cleanPhone);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            // Profile saved. Reload the session; this normally ends on the Dashboard.
            // If something goes wrong the user is signed out and Login shows the reason.
            SessionLoadResult session = await _session.LoadAsync(true);
            await _navigation.GoToAsync(session.Route ?? Routes.Login);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
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

    private static string? Validate(string name, string phone)
    {
        if (name.Length == 0)
        {
            return "Please enter your name.";
        }

        if (name.Length > MaxNameLength)
        {
            return "Name is too long.";
        }

        if (phone.Length == 0)
        {
            return "Please enter your phone number.";
        }

        if (!InputValidator.IsValidPhone(phone))
        {
            return "Please enter a valid phone number (7 to 15 digits).";
        }

        return null;
    }
}
