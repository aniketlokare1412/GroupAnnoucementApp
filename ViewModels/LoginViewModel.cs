using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public LoginViewModel(INavigationService navigation, IAuthService authService)
    {
        _navigation = navigation;
        _authService = authService;
    }

    // Called by the page every time it appears, so a previous
    // user's email/password never stays on screen after logout.
    public void Reset()
    {
        Email = string.Empty;
        Password = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        string cleanEmail = (Email ?? string.Empty).Trim();
        string? validationError = Validate(cleanEmail, Password);
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

            Password = string.Empty;
            await _navigation.GoToAsync(Routes.Dashboard);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToRegisterAsync()
    {
        return _navigation.GoToAsync(Routes.Register);
    }

    private static string? Validate(string email, string? password)
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
}
