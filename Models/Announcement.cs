using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}/announcements/{announcementId}
// When an admin posts to several groups at once, each group gets its OWN copy.
// The copies share the same BatchId so they can be linked later (for example in Feature 8).
public sealed class Announcement : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    // The group this copy belongs to (also implied by the document path).
    [FirestoreProperty("groupId")]
    public string GroupId { get; set; } = string.Empty;

    // Same value on every copy created by one "post to several groups" action.
    // Empty when the announcement was posted to a single group.
    [FirestoreProperty("batchId")]
    public string BatchId { get; set; } = string.Empty;

    [FirestoreProperty("title")]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty("message")]
    public string Message { get; set; } = string.Empty;

    // Soft delete: false means deleted. Fail closed, like groups: a missing field counts as deleted.
    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; } = false;

    // True once the title or message has been changed after posting.
    [FirestoreProperty("isEdited")]
    public bool IsEdited { get; set; } = false;

    // The uid of the admin who posted it. Only this admin may edit or delete it.
    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
