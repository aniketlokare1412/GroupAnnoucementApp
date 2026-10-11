using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IAttachmentRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string groupId, string announcementId, AttachmentDocument file);

    // Returns null when the announcement has no file.
    Task<AttachmentDocument?> GetAsync(string groupId, string announcementId);

    Task DeleteAsync(string groupId, string announcementId);
}
