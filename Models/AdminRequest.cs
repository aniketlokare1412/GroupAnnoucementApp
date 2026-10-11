using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: adminRequests/{uid}. One open request per person; deleted once decided.
public sealed class AdminRequest : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
