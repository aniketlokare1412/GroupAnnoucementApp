using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IAnnouncementRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    // Creates groups/{groupId}/announcements/{id}. The caller supplies a unique id.
    Task CreateAsync(string groupId, string id, Announcement announcement);

    // Returns null when the document does not exist.
    Task<Announcement?> GetByIdAsync(string groupId, string id);

    // Every announcement of the group, in no particular order.
    // activeOnly = true skips soft-deleted ones. Members MUST use true: the Firestore rules
    // only allow members to run the "isActive == true" query.
    Task<List<Announcement>> GetByGroupAsync(string groupId, bool activeOnly);

    // Changes title and message and marks the announcement as edited.
    Task UpdateAsync(string groupId, string id, string title, string message);

    Task SetActiveAsync(string groupId, string id, bool isActive);
}
