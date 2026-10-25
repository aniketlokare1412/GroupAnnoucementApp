using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Admin-only user operations. The checks here are a convenience for friendly messages;
// the real protection is the Firestore security rules.
public interface IUserAdminService
{
    // All users, sorted by name.
    // Phase 5: kept in memory for 30 minutes; forceRefresh (pull-to-refresh) reads every user again.
    Task<OperationResult<List<UserProfile>>> GetAllUsersAsync(bool forceRefresh = false);

    // The user count of the last full read (kept on the phone), null when never read. Costs no reads.
    int? LastKnownUserCount();

    Task<OperationResult> SetActiveAsync(string uid, bool isActive);

    // userType must be UserTypes.Admin or UserTypes.Regular.
    Task<OperationResult> SetUserTypeAsync(string uid, string userType);
}
