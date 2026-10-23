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
    // "Active" or "Completed". Older announcements have no status (treated as Active).
    [FirestoreProperty("status")]
    public string Status { get; set; } = string.Empty;

    // Minutes after posting when the work should be done. 0 = no time set.
    [FirestoreProperty("resolveMinutes")]
    public int ResolveMinutes { get; set; }

    // When an admin marked the work completed (Unix milliseconds). 0 = not completed.
    [FirestoreProperty("completedAtMs")]
    public long CompletedAtMs { get; set; }

    // One optional attachment (an image or a document); the file itself is a separate document under
    // this announcement (see AttachmentDocument). Empty strings when there is none.
    [FirestoreProperty("attachmentName")]
    public string AttachmentName { get; set; } = string.Empty;

    // "image", "file" or "".
    [FirestoreProperty("attachmentKind")]
    public string AttachmentKind { get; set; } = string.Empty;

    // Phase 4: the file of a broadcast is stored ONCE (sharedAttachments/{sharedFileId}) and every group's
    // announcement points at it. Empty = no shared file (no file, or an older per-group copy).
    [FirestoreProperty("sharedFileId")]
    public string SharedFileId { get; set; } = string.Empty;

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
