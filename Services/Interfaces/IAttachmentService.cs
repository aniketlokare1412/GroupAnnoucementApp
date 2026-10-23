using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Announcement attachments (one file, up to 200 KB), stored inside Firestore.
public interface IAttachmentService
{
    Task<OperationResult<UploadedAttachment>> UploadAsync(string groupId, string announcementId, AttachmentPick file);

    Task<OperationResult<AttachmentFile>> GetAsync(string groupId, string announcementId);

    // Phase 4: the file of a broadcast is stored once (sharedAttachments/{fileId}) for all its groups.
    Task<OperationResult<UploadedAttachment>> UploadSharedAsync(string fileId, AttachmentPick file);

    Task<OperationResult<AttachmentFile>> GetSharedAsync(string fileId);

    // Best effort: never throws.
    Task DeleteSharedAsync(string fileId);

    // Best effort: never throws. Used when an older announcement is replaced.
    Task DeleteAsync(string groupId, string announcementId);
}
