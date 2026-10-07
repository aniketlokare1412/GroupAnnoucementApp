using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class SessionService : ISessionService
{
    private const string DeactivatedMessage = "Your account has been deactivated. Please contact your admin.";

    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    private UserProfile? _currentProfile;
    private string? _notice;

    public SessionService(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    public UserProfile? CurrentProfile
    {
        get { return _currentProfile; }
    }

    public bool IsAdmin
    {
        get
        {
            if (_currentProfile == null)
            {
                return false;
            }
            return string.Equals(_currentProfile.UserType, UserTypes.Admin, StringComparison.Ordinal);
        }
    }

    public async Task<SessionLoadResult> LoadAsync(bool signOutOnProblem)
    {
        string? uid = _authService.GetCurrentUserId();
        if (!_authService.IsAuthenticated || uid == null)
        {
            _currentProfile = null;
            return SessionLoadResult.Create(SessionStatus.SignedOut, null, Routes.Login);
        }

        OperationResult<UserProfile?> profileResult = await _userService.GetProfileAsync(uid);

        if (!profileResult.IsSuccess)
        {
            _currentProfile = null;
            string failMessage = profileResult.ErrorMessage ?? "We could not load your profile.";

            if (signOutOnProblem)
            {
                await SignOutAsync();
                _notice = failMessage;
                return SessionLoadResult.Create(SessionStatus.LoadFailed, failMessage, Routes.Login);
            }

            return SessionLoadResult.Create(SessionStatus.LoadFailed, failMessage, null);
        }

        UserProfile? profile = profileResult.Value;

        if (profile == null)
        {
            _currentProfile = null;
            return SessionLoadResult.Create(SessionStatus.ProfileMissing, null, Routes.CompleteProfile);
        }

        if (!profile.IsActive)
        {
            _currentProfile = null;
            await SignOutAsync();
            _notice = DeactivatedMessage;
            return SessionLoadResult.Create(SessionStatus.Deactivated, DeactivatedMessage, Routes.Login);
        }

        _currentProfile = profile;
        return SessionLoadResult.Create(SessionStatus.Ready, null, Routes.Dashboard);
    }

    public async Task<OperationResult> SignOutAsync()
    {
        OperationResult result = await _authService.LogoutAsync();
        if (result.IsSuccess)
        {
            _currentProfile = null;
        }
        return result;
    }

    public string? TakeNotice()
    {
        string? notice = _notice;
        _notice = null;
        return notice;
    }
}
