using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: communities/{communityId}
// A community collects groups (many-to-many: a group can be in several communities) and
// belongs to at most one super community. Both links are optional.
public sealed class Community : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    // Same idea as the group number: a positive whole number, unique across communities.
    [FirestoreProperty("number")]
    public string Number { get; set; } = string.Empty;

    [FirestoreProperty("description")]
    public string Description { get; set; } = string.Empty;

    // The ids of the groups in this community.
    [FirestoreProperty("groupIds")]
    public List<string> GroupIds { get; set; } = new List<string>();

    // The id of the super community this community sits under. Empty = none.
    [FirestoreProperty("superCommunityId")]
    public string SuperCommunityId { get; set; } = string.Empty;

    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
