using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class AdminRequestService : IAdminRequestService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IUserListCache _userCache;
    private readonly IAdminRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;

    public AdminRequestService(
        ISessionService session,
        IAuthService authService,
        IAdminRequestRepository requestRepository,
        IUserRepository userRepository,
        IUserListCache userCache)
    {
        _userCache = userCache;
        _session = session;
        _authService = authService;
        _requestRepository = requestRepository;
        _userRepository = userRepository;
    }

    public async Task<OperationResult<bool>> HasPendingRequestAsync()
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null || _session.IsAdmin)
        {
            return OperationResult<bool>.Success(false);
        }

        try
        {
            AdminRequest? existing = await ServiceHelper.WithTimeoutAsync(_requestRepository.GetByIdAsync(uid));
            return OperationResult<bool>.Success(existing != null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CHECK ADMIN REQUEST ERROR: " + ex);
            return OperationResult<bool>.Fail(ServiceHelper.MapError(ex, "We could not check your request."));
        }
    }

    public async Task<OperationResult> RequestAsync()
    {
        string? uid = _authService.GetCurrentUserId();
        UserProfile? profile = _session.CurrentProfile;
        if (uid == null || profile == null)
        {
            return OperationResult.Fail("Please sign in again.");
        }

        if (_session.IsAdmin)
        {
            return OperationResult.Fail("You are already an administrator.");
        }

        try
        {
            AdminRequest? existing = await ServiceHelper.WithTimeoutAsync(_requestRepository.GetByIdAsync(uid));
            if (existing != null)
            {
                return OperationResult.Success();
            }

            AdminRequest request = new AdminRequest();
            request.UserId = uid;
            request.Phone = profile.Phone;
            await ServiceHelper.WithTimeoutAsync(_requestRepository.CreateAsync(uid, request));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SEND ADMIN REQUEST ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not send your request. Please try again."));
        }
    }

    public async Task<OperationResult<List<AdminRequest>>> GetPendingAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<AdminRequest>>.Fail("Only administrators can see admin requests.");
        }

        try
        {
            List<AdminRequest> items = await ServiceHelper.WithTimeoutAsync(_requestRepository.GetAllAsync());
            items.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
            return OperationResult<List<AdminRequest>>.Success(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ADMIN REQUESTS ERROR: " + ex);
            return OperationResult<List<AdminRequest>>.Fail(ServiceHelper.MapError(ex, "We could not load the admin requests. Please try again."));
        }
    }

    public async Task<OperationResult> ApproveAsync(string uid)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult.Fail("Only administrators can approve requests.");
        }

        if (string.IsNullOrEmpty(uid) || uid == _authService.GetCurrentUserId())
        {
            return OperationResult.Fail("This request cannot be approved.");
        }

        try
        {
            await ServiceHelper.WithTimeoutAsync(_userRepository.SetUserTypeAsync(uid, UserTypes.Admin));
            _userCache.Patch(uid, null, UserTypes.Admin);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("APPROVE ADMIN REQUEST ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not approve the request. Please try again."));
        }

        // The person is an admin now. If closing the request fails, they still are one.
        return await CloseAsync(uid);
    }

    public async Task<OperationResult> DeclineAsync(string uid)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult.Fail("Only administrators can decline requests.");
        }

        return await CloseAsync(uid);
    }

    private async Task<OperationResult> CloseAsync(string uid)
    {
        try
        {
            await ServiceHelper.WithTimeoutAsync(_requestRepository.DeleteAsync(uid));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CLOSE ADMIN REQUEST ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not close the request. Please try again."));
        }
    }
}
