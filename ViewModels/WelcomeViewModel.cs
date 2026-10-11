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
    private string _phone = string.Empty;

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
        Phone = string.Empty;
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

        string cleanPhone = PhoneLogin.Normalize(Phone ?? string.Empty);
        string cleanEmail = PhoneLogin.ToEmail(cleanPhone);
        string? validationError = ValidateLogin(cleanPhone, Password);
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

        string cleanPhone = PhoneLogin.Normalize(Phone ?? string.Empty);
        // Lower-case so it always matches the email Firebase puts in the auth token.
        string cleanEmail = PhoneLogin.ToEmail(cleanPhone);

        string? validationError = ValidateRegister(cleanPhone, Password, ConfirmPassword);
        if (validationError != null)
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _userService.RegisterAsync(cleanPhone, cleanPhone, cleanEmail, Password);
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

    private static string? ValidateLogin(string phone, string? password)
    {
        string? phoneProblem = ValidatePhone(phone);
        if (phoneProblem != null)
        {
            return phoneProblem;
        }

        if (string.IsNullOrEmpty(password))
        {
            return "Please enter your password.";
        }

        return null;
    }

    private static string? ValidateRegister(string phone, string? password, string? confirmPassword)
    {
        string? phoneProblem = ValidatePhone(phone);
        if (phoneProblem != null)
        {
            return phoneProblem;
        }

        if (string.IsNullOrEmpty(password))
        {
            return "Please enter a password.";
        }

        if (password.Length < MinPasswordLength)
        {
            return "Password must be at least " + MinPasswordLength + " characters.";
        }

        if (password != confirmPassword)
        {
            return "The two passwords do not match.";
        }

        return null;
    }

    private static string? ValidatePhone(string phone)
    {
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
