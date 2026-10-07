using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Admin-only user operations. The checks here are a convenience for friendly messages;
// the real protection is the Firestore security rules.
public interface IUserAdminService
{
    // All users, sorted by name.
    Task<OperationResult<List<UserProfile>>> GetAllUsersAsync();

    Task<OperationResult> SetActiveAsync(string uid, bool isActive);

    // userType must be UserTypes.Admin or UserTypes.Regular.
    Task<OperationResult> SetUserTypeAsync(string uid, string userType);
}
