using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IAdminRequestRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string uid, AdminRequest request);

    // Returns null when there is no request.
    Task<AdminRequest?> GetByIdAsync(string uid);

    // Admin only (enforced by the Firestore rules).
    Task<List<AdminRequest>> GetAllAsync();

    Task DeleteAsync(string uid);
}
