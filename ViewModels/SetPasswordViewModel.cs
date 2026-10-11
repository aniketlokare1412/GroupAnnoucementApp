using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Shown once to a person who has just been made an admin: they choose their own password.
public partial class SetPasswordViewModel : BaseViewModel
{
    private const int MinPasswordLength = 6;

    private readonly INavigationService _navigation;
    private readonly IUserService _userService;
    private readonly ISessionService _session;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    public SetPasswordViewModel(INavigationService navigation, IUserService userService, ISessionService session)
    {
        _navigation = navigation;
        _userService = userService;
        _session = session;
    }

    public void Prepare()
    {
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < MinPasswordLength)
        {
            ErrorMessage = "Password must be at least " + MinPasswordLength + " characters.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "The two passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            OperationResult result = await _userService.SetAdminPasswordAsync(NewPassword);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            SessionLoadResult session = await _session.LoadAsync(true);
            await _navigation.GoToAsync(session.Route ?? Routes.Splash);
        }
        finally
        {
            IsBusy = false;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await _session.SignOutAsync();
        await _navigation.GoToAsync(Routes.Login);
    }
}
