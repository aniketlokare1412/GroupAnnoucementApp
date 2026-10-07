using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}/members/{uid}
// The document id is the member's uid, so a user can be a member of a group only once.
public sealed class GroupMember : IFirestoreObject
{
    // The document id (the member's uid).
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    // Also stored as a field: the "My groups" query is a collection group query
    // on members where userId == the signed-in uid.
    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    // Stored so a collection group query result tells us which group it belongs to.
    [FirestoreProperty("groupId")]
    public string GroupId { get; set; } = string.Empty;

    // Who created the membership: the admin, or the user themselves when they joined.
    [FirestoreProperty("addedBy")]
    public string AddedBy { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("addedAt")]
    public DateTimeOffset AddedAt { get; set; }
}
