using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// One screen for both "Sign in" and "Create account" (replaces LoginViewModel and RegisterViewModel).
public partial class WelcomeViewModel : BaseViewModel
{
    private const int MinPasswordLength = 6;
    private const int MaxNameLength = 100;

    private readonly INavigationService _navigation;
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ISessionService _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignInMode))]
    [NotifyPropertyChangedFor(nameof(SubheadingText))]
    [NotifyPropertyChangedFor(nameof(SubmitText))]
    private bool _isRegisterMode;

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

    public bool IsSignInMode => !IsRegisterMode;

    public string SubheadingText
    {
        get
        {
            if (IsRegisterMode)
            {
                return "Create an account to join your groups.";
            }

            return "Sign in to see your group announcements.";
        }
    }

    public string SubmitText
    {
        get { return IsRegisterMode ? "Create account" : "Log in"; }
    }

    public WelcomeViewModel(
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

    // Called by the page every time it appears, so a previous user's details never stay on screen
    // after logout. Also shows a pending notice (for example "your account has been deactivated").
    public void Reset()
    {
        IsRegisterMode = false;
        Name = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        ClearSecrets();
        ErrorMessage = _session.TakeNotice();
    }

    [RelayCommand]
    private void ShowSignIn()
    {
        if (IsBusy)
        {
            return;
        }

        IsRegisterMode = false;
        ErrorMessage = null;
        ClearSecrets();
    }

    [RelayCommand]
    private void ShowRegister()
    {
        if (IsBusy)
        {
            return;
        }

        IsRegisterMode = true;
        ErrorMessage = null;
        ClearSecrets();
    }

    [RelayCommand]
    private Task SubmitAsync()
    {
        if (IsRegisterMode)
        {
            return RegisterAsync();
        }

        return LoginAsync();
    }

    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanEmail = (Email ?? string.Empty).Trim();
        string? validationError = ValidateLogin(cleanEmail, Password);
        if (validationError != null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _authService.LoginAsync(cleanEmail, Password);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            await FinishSignInAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

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

        string? validationError = ValidateRegister(cleanName, cleanPhone, cleanEmail, Password, ConfirmPassword);
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

            await FinishSignInAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Signed in (or just registered). Load the profile and go to the right home screen.
    private async Task FinishSignInAsync()
    {
        SessionLoadResult session = await _session.LoadAsync(true);

        if (session.Status == SessionStatus.Ready || session.Status == SessionStatus.ProfileMissing)
        {
            ClearSecrets();
            await _navigation.GoToAsync(session.Route ?? Routes.Splash);
            return;
        }

        // Deactivated or load failed: the user was signed out. Show the reason here.
        ErrorMessage = _session.TakeNotice() ?? session.Message;
    }

    private void ClearSecrets()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    private static string? ValidateLogin(string email, string? password)
    {
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
            return "Please enter your password.";
        }

        return null;
    }

    private static string? ValidateRegister(string name, string phone, string email, string? password, string? confirmPassword)
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
