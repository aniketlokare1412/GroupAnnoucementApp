using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class UserService : IUserService
{
    private const int ProfileWriteTimeoutSeconds = 20;
    private const int ProfileReadTimeoutSeconds = 15;

    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public UserService(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    public async Task<OperationResult> RegisterAsync(string name, string phone, string email, string password)
    {
        // Step 1: create the Auth account (this also signs the user in).
        OperationResult<string> authResult = await _authService.RegisterAsync(email, password);
        if (!authResult.IsSuccess)
        {
            return OperationResult.Fail(authResult.ErrorMessage ?? "Registration failed. Please try again.");
        }

        string uid = authResult.Value ?? string.Empty;

        // Step 2: create users/{uid}.
        UserProfile profile = BuildNewProfile(name, phone, email);
        string? writeError = await WriteProfileAsync(uid, profile);
        if (writeError != null)
        {
            // Roll back so the user is not left with an account but no profile.
            await _authService.DeleteCurrentUserAsync();
            return OperationResult.Fail(writeError);
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult<UserProfile?>> GetProfileAsync(string uid)
    {
        try
        {
            Task<UserProfile?> readTask = _userRepository.GetByIdAsync(uid);
            Task finished = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(ProfileReadTimeoutSeconds)));

            if (finished != readTask)
            {
                return OperationResult<UserProfile?>.Fail("Loading your profile is taking too long. Please check your connection and try again.");
            }

            UserProfile? profile = await readTask;
            return OperationResult<UserProfile?>.Success(profile);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PROFILE LOAD ERROR: " + ex);
            return OperationResult<UserProfile?>.Fail("We could not load your profile. Please check your connection and try again.");
        }
    }

    public async Task<OperationResult> CreateOwnProfileAsync(string name, string phone)
    {
        string? uid = _authService.GetCurrentUserId();
        string? email = _authService.GetCurrentUserEmail();
        if (uid == null || email == null)
        {
            return OperationResult.Fail("Your session has expired. Please log in again.");
        }

        UserProfile profile = BuildNewProfile(name, phone, email.Trim().ToLowerInvariant());
        string? writeError = await WriteProfileAsync(uid, profile);
        if (writeError != null)
        {
            return OperationResult.Fail(writeError);
        }

        return OperationResult.Success();
    }

    private static UserProfile BuildNewProfile(string name, string phone, string email)
    {
        // The client can only ever create a Regular user; the Firestore rules enforce this too.
        UserProfile profile = new UserProfile();
        profile.Name = name;
        profile.Phone = phone;
        profile.Email = email;
        profile.UserType = UserTypes.Regular;
        profile.IsActive = true;
        return profile;
    }

    // Returns null on success, otherwise a friendly error message.
    private async Task<string?> WriteProfileAsync(string uid, UserProfile profile)
    {
        try
        {
            Task createTask = _userRepository.CreateAsync(uid, profile);
            Task finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(ProfileWriteTimeoutSeconds)));

            if (finished != createTask)
            {
                return "Saving your profile is taking too long. Please check your connection and try again.";
            }

            // Re-awaiting surfaces any exception thrown by the write.
            await createTask;
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PROFILE WRITE ERROR: " + ex);
            return "We could not save your profile. Please try again.";
        }
    }
}
