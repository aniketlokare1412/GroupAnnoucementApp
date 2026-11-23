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
    private readonly IAdminProfileRepository _adminProfiles;

    public AdminRequestService(
        ISessionService session,
        IAuthService authService,
        IAdminRequestRepository requestRepository,
        IUserRepository userRepository,
        IUserListCache userCache,
        IAdminProfileRepository adminProfiles)
    {
        _adminProfiles = adminProfiles;
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

    public async Task<OperationResult> RequestAsync(AdminRequestDetails details)
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

        string name = (details.Name ?? string.Empty).Trim();
        string designation = (details.Designation ?? string.Empty).Trim();
        string cpf = (details.CpfNumber ?? string.Empty).Trim();
        string division = (details.Division ?? string.Empty).Trim();
        string subDivision = (details.SubDivision ?? string.Empty).Trim();
        string section = (details.Section ?? string.Empty).Trim();
        string subStation = (details.SubStationCode ?? string.Empty).Trim();

        // All fields are required; the only rule is no special characters (spaces are fine).
        string? problem = AdminRequestLimits.CheckText("your name", name);
        if (problem == null && !AdminRequestLimits.IsValidDesignation(designation))
        {
            problem = "Choose your designation.";
        }

        problem = problem
            ?? AdminRequestLimits.CheckText("your CPF number", cpf)
            ?? AdminRequestLimits.CheckText("your division", division)
            ?? AdminRequestLimits.CheckText("your sub division", subDivision)
            ?? AdminRequestLimits.CheckText("your section", section)
            ?? AdminRequestLimits.CheckText("your sub station code", subStation);

        if (problem == null && details.IdCard != null)
        {
            string type = details.IdCard.ContentType;
            if (type != "image/jpeg" && type != "image/png")
            {
                problem = "The ID card photo must be a JPEG or PNG image.";
            }
            else if (details.IdCard.Bytes.Length > AttachmentLimits.MaxBytes)
            {
                problem = "The ID card photo is too large. Please choose a smaller one.";
            }
        }

        if (problem != null)
        {
            return OperationResult.Fail(problem);
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
            request.Name = name;
            request.Designation = designation;
            request.CpfNumber = cpf;
            request.Division = division;
            request.SubDivision = subDivision;
            request.Section = section;
            request.SubStationCode = subStation;
            if (details.IdCard != null)
            {
                request.IdCardName = details.IdCard.FileName;
                request.IdCardContentType = details.IdCard.ContentType;
                request.IdCardData = details.IdCard.Base64;
            }

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

    // Copies the request's details to adminProfiles/{uid}. Never blocks the approval: an admin without details shows a name only.
    private async Task SaveAdminProfileAsync(string uid)
    {
        try
        {
            AdminRequest? request = await ServiceHelper.WithTimeoutAsync(_requestRepository.GetByIdAsync(uid));
            if (request == null || string.IsNullOrWhiteSpace(request.Designation))
            {
                return;
            }

            AdminProfile profile = new AdminProfile();
            profile.Id = uid;
            profile.Name = request.Name;
            profile.Designation = request.Designation;
            profile.CpfNumber = request.CpfNumber;
            profile.Division = request.Division;
            profile.SubDivision = request.SubDivision;
            profile.Section = request.Section;
            profile.SubStationCode = request.SubStationCode;
            await ServiceHelper.WithTimeoutAsync(_adminProfiles.SetAsync(uid, profile));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SAVE ADMIN PROFILE ERROR: " + ex);
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

        // The details from the form are kept as the admin's profile (shown with the messages they publish).
        await SaveAdminProfileAsync(uid);

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
