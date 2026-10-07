using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IUserRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string uid, UserProfile profile);

    // Returns null when the document does not exist.
    Task<UserProfile?> GetByIdAsync(string uid);

    // Admin only (enforced by the Firestore rules).
    Task<List<UserProfile>> GetAllAsync();

    Task SetActiveAsync(string uid, bool isActive);

    Task SetUserTypeAsync(string uid, string userType);
}
