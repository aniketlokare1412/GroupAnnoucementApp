using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}/announcements/{announcementId}/panics/{uid}
// One per person (the document id is the person's uid), so nobody can press twice.
public sealed class PanicPress : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
