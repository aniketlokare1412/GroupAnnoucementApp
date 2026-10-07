using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class UserAdminService : IUserAdminService
{
    private const int TimeoutSeconds = 20;

    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public UserAdminService(ISessionService session, IAuthService authService, IUserRepository userRepository)
    {
        _session = session;
        _authService = authService;
        _userRepository = userRepository;
    }

    public async Task<OperationResult<List<UserProfile>>> GetAllUsersAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<UserProfile>>.Fail("You do not have permission to view users.");
        }

        try
        {
            Task<List<UserProfile>> loadTask = _userRepository.GetAllAsync();
            Task finished = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));

            if (finished != loadTask)
            {
                return OperationResult<List<UserProfile>>.Fail("Loading users is taking too long. Please check your connection and try again.");
            }

            List<UserProfile> users = await loadTask;
            users.Sort(CompareByName);
            return OperationResult<List<UserProfile>>.Success(users);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD USERS ERROR: " + ex);
            return OperationResult<List<UserProfile>>.Fail(MapError(ex, "We could not load the users. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult> SetActiveAsync(string uid, bool isActive)
    {
        string? problem = CheckCanModify(uid);
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        return await RunUpdateAsync(_userRepository.SetActiveAsync(uid, isActive));
    }

    public async Task<OperationResult> SetUserTypeAsync(string uid, string userType)
    {
        if (userType != UserTypes.Admin && userType != UserTypes.Regular)
        {
            return OperationResult.Fail("Unknown role.");
        }

        string? problem = CheckCanModify(uid);
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        return await RunUpdateAsync(_userRepository.SetUserTypeAsync(uid, userType));
    }

    // Returns null when the change is allowed, otherwise a friendly message.
    private string? CheckCanModify(string uid)
    {
        if (!_session.IsAdmin)
        {
            return "You do not have permission to change users.";
        }

        if (string.IsNullOrEmpty(uid))
        {
            return "Unknown user.";
        }

        if (uid == _authService.GetCurrentUserId())
        {
            return "You cannot change your own role or status.";
        }

        // Without a connection Firestore would queue the write and apply it later,
        // which is confusing for an admin action. So we refuse up front.
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return "You are offline. Please connect to the internet and try again.";
        }

        return null;
    }

    private static async Task<OperationResult> RunUpdateAsync(Task updateTask)
    {
        try
        {
            Task finished = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));

            if (finished != updateTask)
            {
                return OperationResult.Fail("The request is taking too long. Refresh the list to see the current status.");
            }

            await updateTask;
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE USER ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not save the change. Please try again."));
        }
    }

    private static string MapError(Exception ex, string fallbackMessage)
    {
        string text = (ex.GetType().FullName + " " + ex.Message).ToLowerInvariant();

        if (text.Contains("permission"))
        {
            return "You do not have permission to do that.";
        }

        return fallbackMessage;
    }

    private static int CompareByName(UserProfile a, UserProfile b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }
}
