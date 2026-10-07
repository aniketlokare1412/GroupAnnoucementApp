using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

public interface ISessionService
{
    // The signed-in user's profile. Null when signed out or not loaded yet.
    UserProfile? CurrentProfile { get; }

    // True only when the loaded profile has userType exactly "Admin".
    bool IsAdmin { get; }

    // Checks the Firebase session, loads users/{uid} and decides where to go next.
    // If signOutOnProblem is true, a deactivated account or a failed load also signs the user out
    // (and leaves a notice for the Login page). A deactivated account is ALWAYS signed out.
    Task<SessionLoadResult> LoadAsync(bool signOutOnProblem);

    // Signs out of Firebase and clears the cached profile.
    Task<OperationResult> SignOutAsync();

    // Returns the pending notice (for example "account deactivated") once, then clears it.
    string? TakeNotice();
}
