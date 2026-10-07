using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IUserRepository
{
    // Throws on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string uid, UserProfile profile);
}
