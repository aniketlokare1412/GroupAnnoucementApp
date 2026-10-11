using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}/announcements/{announcementId}/attachments/main
// The file itself is stored as text (base64) in "data", so no paid Storage plan is needed.
public sealed class AttachmentDocument : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [FirestoreProperty("size")]
    public long Size { get; set; }

    [FirestoreProperty("data")]
    public string Data { get; set; } = string.Empty;

    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;
}
