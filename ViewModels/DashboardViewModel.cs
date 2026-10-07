using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    public DashboardViewModel(INavigationService navigation, IAuthService authService)
    {
        _navigation = navigation;
        _authService = authService;
    }

    // Called by the page every time it appears (the page instance is
    // reused by Shell, so the constructor alone would show stale data).
    public void LoadUser()
    {
        ErrorMessage = null;
        UserEmail = _authService.GetCurrentUserEmail() ?? string.Empty;
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
            OperationResult result = await _authService.LogoutAsync();
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
