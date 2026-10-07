using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private const int MinPasswordLength = 6;
    private const int MaxNameLength = 100;

    private readonly INavigationService _navigation;
    private readonly IUserService _userService;
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    public RegisterViewModel(INavigationService navigation, IUserService userService, ISessionService session)
    {
        _navigation = navigation;
        _userService = userService;
        _session = session;
    }

    public void Reset()
    {
        Name = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanName = (Name ?? string.Empty).Trim();
        string cleanPhone = (Phone ?? string.Empty).Trim();
        // Lower-case so it always matches the email Firebase puts in the auth token.
        string cleanEmail = (Email ?? string.Empty).Trim().ToLowerInvariant();

        string? validationError = Validate(cleanName, cleanPhone, cleanEmail, Password, ConfirmPassword);
        if (validationError != null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _userService.RegisterAsync(cleanName, cleanPhone, cleanEmail, Password);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            // Account and profile exist. Load the session and go to the right screen.
            SessionLoadResult session = await _session.LoadAsync(true);

            if (session.Status == SessionStatus.Ready || session.Status == SessionStatus.ProfileMissing)
            {
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                await _navigation.GoToAsync(session.Route ?? Routes.Dashboard);
                return;
            }

            // The account was created but the profile could not be loaded: user was signed out.
            ErrorMessage = _session.TakeNotice() ?? session.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToLoginAsync()
    {
        return _navigation.GoToAsync(Routes.Login);
    }

    private static string? Validate(string name, string phone, string email, string? password, string? confirmPassword)
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

        if (email.Length == 0)
        {
            return "Please enter your email.";
        }

        if (!InputValidator.IsValidEmail(email))
        {
            return "Please enter a valid email address.";
        }

        if (string.IsNullOrEmpty(password))
        {
            return "Please enter a password.";
        }

        if (password.Length < MinPasswordLength)
        {
            return "Password must be at least 6 characters.";
        }

        if (password != confirmPassword)
        {
            return "Passwords do not match.";
        }

        return null;
    }
}
