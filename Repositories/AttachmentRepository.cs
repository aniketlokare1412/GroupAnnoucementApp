using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    // One file per announcement, always stored under this id.
    private const string FileId = "main";

    private readonly IFirebaseFirestore _firestore;

    public AttachmentRepository(IFirebaseFirestore firestore)
    {
        _firestore = firestore;
    }

    public Task CreateAsync(string groupId, string announcementId, AttachmentDocument file)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId, announcementId));
        return document.SetDataAsync(file);
    }

    public async Task<AttachmentDocument?> GetAsync(string groupId, string announcementId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId, announcementId));
        IDocumentSnapshot<AttachmentDocument> snapshot = await document.GetDocumentSnapshotAsync<AttachmentDocument>();
        return snapshot.Data;
    }

    public Task DeleteAsync(string groupId, string announcementId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId, announcementId));
        return document.DeleteDocumentAsync();
    }

    private static string PathFor(string groupId, string announcementId)
    {
        return "groups/" + groupId + "/announcements/" + announcementId + "/attachments/" + FileId;
    }
}
