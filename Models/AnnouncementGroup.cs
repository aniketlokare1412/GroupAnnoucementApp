using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}
// Members will live in groups/{groupId}/members/{uid} (Feature 6).
public sealed class AnnouncementGroup : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("description")]
    public string Description { get; set; } = string.Empty;

    // Fail closed: a group with a missing isActive field counts as NOT active.
    // Code that creates a group sets this to true explicitly.
    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; } = false;

    // The uid of the admin who created the group.
    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
