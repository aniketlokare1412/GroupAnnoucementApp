using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Phase 2. Firestore document: userGroups/{uid}
// The ids of the groups a user is a member of, so Home needs no collection group query over all members.
// Kept in step with groups/{g}/members/{uid} by join, leave and the admin add / remove member actions.
// A user without this document is repaired the first time the app needs it (see UserGroupsService).
public sealed class UserGroupsDocument : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("groupIds")]
    public List<string> GroupIds { get; set; } = new List<string>();

    [FirestoreServerTimestamp("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
