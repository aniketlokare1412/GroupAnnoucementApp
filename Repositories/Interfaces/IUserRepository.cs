using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IUserRepository
{
    // Both methods throw on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string uid, UserProfile profile);

    // Returns null when the document does not exist.
    Task<UserProfile?> GetByIdAsync(string uid);
}
