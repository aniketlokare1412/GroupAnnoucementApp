using GroupAnnouncementApp.Helpers;
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
        FirestoreMeter.Write("Attachment.CreateAsync");
        return document.SetDataAsync(file);
    }

    public async Task<AttachmentDocument?> GetAsync(string groupId, string announcementId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId, announcementId));
        IDocumentSnapshot<AttachmentDocument> snapshot = await document.GetDocumentSnapshotAsync<AttachmentDocument>();
        FirestoreMeter.Read("Attachment.GetAsync", 1);
        return snapshot.Data;
    }

    public Task DeleteAsync(string groupId, string announcementId)
    {
        IDocumentReference document = _firestore.GetDocument(PathFor(groupId, announcementId));
        FirestoreMeter.Delete("Attachment.DeleteAsync");
        return document.DeleteDocumentAsync();
    }

    public Task CreateSharedAsync(string fileId, AttachmentDocument file)
    {
        IDocumentReference document = _firestore.GetDocument(SharedPathFor(fileId));
        FirestoreMeter.Write("Attachment.CreateSharedAsync");
        return document.SetDataAsync(file);
    }

    public async Task<AttachmentDocument?> GetSharedAsync(string fileId)
    {
        IDocumentReference document = _firestore.GetDocument(SharedPathFor(fileId));
        IDocumentSnapshot<AttachmentDocument> snapshot = await document.GetDocumentSnapshotAsync<AttachmentDocument>();
        FirestoreMeter.Read("Attachment.GetSharedAsync", 1);
        return snapshot.Data;
    }

    public Task DeleteSharedAsync(string fileId)
    {
        IDocumentReference document = _firestore.GetDocument(SharedPathFor(fileId));
        FirestoreMeter.Delete("Attachment.DeleteSharedAsync");
        return document.DeleteDocumentAsync();
    }

    private static string SharedPathFor(string fileId)
    {
        return "sharedAttachments/" + fileId;
    }

    private static string PathFor(string groupId, string announcementId)
    {
        return "groups/" + groupId + "/announcements/" + announcementId + "/attachments/" + FileId;
    }
}
