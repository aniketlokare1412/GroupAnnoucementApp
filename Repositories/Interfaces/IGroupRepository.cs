using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IGroupRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    // Creates groups/{id}. The caller supplies a unique id.
    Task CreateAsync(string id, AnnouncementGroup group);

    // Returns null when the document does not exist.
    Task<AnnouncementGroup?> GetByIdAsync(string id);

    // Admin only (enforced by the Firestore rules).
    Task<List<AnnouncementGroup>> GetAllAsync();

    // Changes name and description in one write.
    Task UpdateDetailsAsync(string id, string name, string description);

    Task SetActiveAsync(string id, bool isActive);
}
