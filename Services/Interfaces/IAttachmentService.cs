using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Announcement attachments (one file, up to 700 KB), stored inside Firestore.
public interface IAttachmentService
{
    Task<OperationResult<UploadedAttachment>> UploadAsync(string groupId, string announcementId, AttachmentPick file);

    Task<OperationResult<AttachmentFile>> GetAsync(string groupId, string announcementId);

    // Best effort: never throws. Used when an older announcement is replaced.
    Task DeleteAsync(string groupId, string announcementId);
}
