using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;

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

    // Sets the status ("Active" or "Completed") and the completion time.
    Task SetStatusAsync(string groupId, string id, string status, long completedAtMs);

    // Removes the document for good (used to drop older announcements when a new one is posted).
    Task DeleteAsync(string groupId, string id);

    // Phase 4: adds the create / delete to a batch instead of sending it.
    void QueueCreate(FirestoreBatch batch, string groupId, string id, Announcement announcement);

    void QueueDelete(FirestoreBatch batch, string groupId, string id);

    // True when some announcement (in any group) still points at this shared file. 1 read.
    // Needs the collection group index on announcements.sharedFileId (see firestore.indexes.json).
    Task<bool> ExistsWithSharedFileAsync(string fileId);
}
