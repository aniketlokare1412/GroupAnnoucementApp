using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// One login page. Members type their phone number only (no password, no registration):
// a new number simply starts a new member account. Admins switch to "Log in with password".
public partial class WelcomeViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ISessionService _session;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMemberMode))]
    [NotifyPropertyChangedFor(nameof(SubheadingText))]
    [NotifyPropertyChangedFor(nameof(SubmitText))]
    private bool _isAdminMode;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public bool IsMemberMode => !IsAdminMode;

    public string SubheadingText
    {
        get
        {
            if (IsAdminMode)
            {
                return "Admin login. Enter your phone number and password.";
            }

            return "Enter your phone number to see your group announcements.";
        }
    }

    public string SubmitText
    {
        get { return IsAdminMode ? "Log in" : "Continue"; }
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

    // Called each time the page appears.
    public void Reset()
    {
        IsAdminMode = false;
        Phone = string.Empty;
        Password = string.Empty;
        ErrorMessage = _session.TakeNotice();
    }

    [RelayCommand]
    private void ShowAdminLogin()
    {
        if (IsBusy)
        {
            return;
        }

        IsAdminMode = true;
        ErrorMessage = null;
        Password = string.Empty;
    }

    [RelayCommand]
    private void ShowMemberLogin()
    {
        if (IsBusy)
        {
            return;
        }

        IsAdminMode = false;
        ErrorMessage = null;
        Password = string.Empty;
    }

    [RelayCommand]
    private Task SubmitAsync()
    {
        if (IsAdminMode)
        {
            return AdminLoginAsync();
        }

        return MemberLoginAsync();
    }

    private async Task AdminLoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanPhone = PhoneLogin.Normalize(Phone ?? string.Empty);
        string? problem = ValidatePhone(cleanPhone);
        if (problem == null && string.IsNullOrEmpty(Password))
        {
            problem = "Please enter your password.";
        }

        if (problem != null)
        {
            ErrorMessage = problem;
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _authService.LoginAsync(PhoneLogin.ToEmail(cleanPhone), Password);
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

    private async Task MemberLoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanPhone = PhoneLogin.Normalize(Phone ?? string.Empty);
        string? problem = ValidatePhone(cleanPhone);
        if (problem != null)
        {
            ErrorMessage = problem;
            return;
        }

        string email = PhoneLogin.ToEmail(cleanPhone);
        string memberPassword = PhoneLogin.MemberPassword(cleanPhone);

        IsBusy = true;
        try
        {
            // Known member: sign in.
            OperationResult login = await _authService.LoginAsync(email, memberPassword);
            if (login.IsSuccess)
            {
                await FinishSignInAsync();
                return;
            }

            // New number: start a member account (the name is the phone number).
            OperationResult created = await _userService.RegisterAsync(cleanPhone, cleanPhone, email, memberPassword);
            if (created.IsSuccess)
            {
                await FinishSignInAsync();
                return;
            }

            string message = created.ErrorMessage ?? "Login failed. Please try again.";
            if (message.Contains("already exists"))
            {
                // The number belongs to an admin (who has a password) or an older account.
                ErrorMessage = "This number needs a password. Tap \"Log in with password\" below.";
                return;
            }

            ErrorMessage = message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FinishSignInAsync()
    {
        SessionLoadResult session = await _session.LoadAsync(true);

        if (session.Status == SessionStatus.Ready || session.Status == SessionStatus.ProfileMissing)
        {
            Password = string.Empty;
            await _navigation.GoToAsync(session.Route ?? Routes.Splash);
            return;
        }

        ErrorMessage = _session.TakeNotice() ?? session.Message;
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
