using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class SplashViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;

    // True when loading failed and the user must choose: try again or log out.
    [ObservableProperty]
    private bool _showActions;

    public SplashViewModel(INavigationService navigation, ISessionService session)
    {
        _navigation = navigation;
        _session = session;
    }

    // Called by the page when it appears. Decides where the app goes at startup.
    public async Task StartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        ShowActions = false;

        try
        {
            SessionLoadResult result = await _session.LoadAsync(false);

            if (result.Status == SessionStatus.LoadFailed)
            {
                ErrorMessage = result.Message;
                ShowActions = true;
                return;
            }

            string route = result.Route ?? Routes.Login;
            await _navigation.GoToAsync(route);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        return StartAsync();
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
}
