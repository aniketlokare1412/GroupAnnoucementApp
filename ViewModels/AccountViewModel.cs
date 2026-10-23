using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// The Account tab: who is signed in, and Log out.
// Log out uses a two-step pill instead of a dialog: the first tap turns it red
// ("Tap again to log out") for a few seconds, the second tap signs out.
public partial class AccountViewModel : BaseViewModel
{
    private const int ConfirmSeconds = 3;

    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAdminRequestService _adminRequests;
    private readonly IToastService _toasts;

    // Bumped every time the confirm state starts or ends, so an old timer never cancels a newer one.
    private int _confirmToken;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private string _roleText = string.Empty;

    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotConfirmingLogout))]
    private bool _isConfirmingLogout;

    public bool IsNotConfirmingLogout => !IsConfirmingLogout;

    // Members can ask an admin to make them an admin; once asked, the button is replaced by a note.
    [ObservableProperty]
    private bool _canRequestAdmin;

    [ObservableProperty]
    private bool _isRequestPending;

    // True for admins (set when the tab appears).
    [ObservableProperty]
    private bool _isAdmin;

    public AccountViewModel(
        INavigationService navigation,
        ISessionService session,
        IAdminRequestService adminRequests,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _adminRequests = adminRequests;
        _toasts = toasts;
    }

    // Called by the page every time it appears (Shell reuses the page instance,
    // so the constructor alone would show stale data after a user switch).
    public async Task RefreshAsync()
    {
        ErrorMessage = null;
        _confirmToken++;
        IsConfirmingLogout = false;

        UserProfile? profile = _session.CurrentProfile;
        if (profile == null)
        {
            // Safety net: never show the account page without a loaded profile.
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        DisplayName = string.IsNullOrWhiteSpace(profile.Phone) ? profile.Name : profile.Phone;
        UserEmail = string.Empty;
        RoleText = _session.IsAdmin ? "Administrator" : "Member";
        IsAdmin = _session.IsAdmin;
        string digits = PhoneLogin.Normalize(DisplayName);
        Initials = digits.Length >= 2 ? digits.Substring(digits.Length - 2) : BuildInitials(DisplayName);

        bool pending = false;
        if (!_session.IsAdmin)
        {
            OperationResult<bool> pendingResult = await _adminRequests.HasPendingRequestAsync();
            pending = pendingResult.IsSuccess && pendingResult.Value;
        }

        IsRequestPending = pending;
        CanRequestAdmin = !_session.IsAdmin && !pending;
    }

    [RelayCommand]
    private async Task RequestAdminAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            OperationResult result = await _adminRequests.RequestAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            IsRequestPending = true;
            CanRequestAdmin = false;
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

        if (!IsConfirmingLogout)
        {
            // First tap: arm the red "Tap again" pill. The timer must NOT be awaited here,
            // or the command would stay "running" and the second tap would be ignored.
            IsConfirmingLogout = true;
            _confirmToken++;
            _ = DisarmAfterDelayAsync(_confirmToken);
            return;
        }

        // Second tap: really log out.
        _confirmToken++;
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
            IsConfirmingLogout = false;
        }
    }

    private async Task DisarmAfterDelayAsync(int token)
    {
        await Task.Delay(TimeSpan.FromSeconds(ConfirmSeconds));
        if (token == _confirmToken)
        {
            IsConfirmingLogout = false;
        }
    }

    private static string BuildInitials(string name)
    {
        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        string first = parts[0].Substring(0, 1);
        if (parts.Length == 1)
        {
            return first.ToUpperInvariant();
        }

        string last = parts[parts.Length - 1].Substring(0, 1);
        return (first + last).ToUpperInvariant();
    }
}
