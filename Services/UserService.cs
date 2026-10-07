using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class UserService : IUserService
{
    private const int ProfileWriteTimeoutSeconds = 20;

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

        // Step 2: create users/{uid}. The client can only ever create a Regular user;
        // the Firestore rules enforce this as well.
        UserProfile profile = new UserProfile();
        profile.Name = name;
        profile.Phone = phone;
        profile.Email = email;
        profile.UserType = UserTypes.Regular;
        profile.IsActive = true;

        try
        {
            Task createTask = _userRepository.CreateAsync(uid, profile);
            Task finished = await Task.WhenAny(createTask, Task.Delay(TimeSpan.FromSeconds(ProfileWriteTimeoutSeconds)));

            if (finished != createTask)
            {
                await _authService.DeleteCurrentUserAsync();
                return OperationResult.Fail("Registration is taking too long. Please check your connection and try again.");
            }

            // Re-awaiting surfaces any exception thrown by the write.
            await createTask;
            return OperationResult.Success();
        }
        catch (Exception)
        {
            // Roll back so the user is not left with an account but no profile.
            await _authService.DeleteCurrentUserAsync();
            return OperationResult.Fail("We could not finish creating your profile. Please try again.");
        }
    }
}
