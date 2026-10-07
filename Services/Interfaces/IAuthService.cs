using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.Services.Interfaces;

public interface IAuthService
{
    bool IsAuthenticated { get; }

    string? GetCurrentUserId();

    string? GetCurrentUserEmail();

    Task<OperationResult> LoginAsync(string email, string password);

    Task<OperationResult> LogoutAsync();

    // Creates the Firebase Auth account and signs the new user in. Returns the new uid.
    Task<OperationResult<string>> RegisterAsync(string email, string password);

    // Used to roll back a half-finished registration. Also signs the user out.
    Task<OperationResult> DeleteCurrentUserAsync();
}
