using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Reading is open to any signed-in user (the Firestore rules decide what they may see).
// Posting, editing and deleting are admin-only, and only the admin who posted an
// announcement may change it. The checks here give friendly messages;
// the real protection is the Firestore security rules.
public interface IAnnouncementService
{
    // Every announcement of a group, newest first. The view model shows them a page at a time.
    // Admins also get deleted ones (flagged IsActive = false); members only get active ones.
    Task<OperationResult<List<Announcement>>> GetForGroupAsync(string groupId);

    Task<OperationResult<Announcement?>> GetAsync(string groupId, string announcementId);

    // Posts one copy of the announcement to every selected (active) group.
    // Success can still contain failed groups: check PublishResult.FailedGroupIds.
    Task<OperationResult<PublishResult>> PublishAsync(string title, string message, List<string> groupIds);

    Task<OperationResult> UpdateAsync(string groupId, string announcementId, string title, string message);

    // Soft delete (isActive = false).
    Task<OperationResult> DeleteAsync(string groupId, string announcementId);
}
