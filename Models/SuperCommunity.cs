using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: superCommunities/{superCommunityId}
// The communities under it are NOT stored here: each community stores its own
// superCommunityId, which is what keeps "a community has exactly one super community" true.
public sealed class SuperCommunity : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("number")]
    public string Number { get; set; } = string.Empty;

    [FirestoreProperty("description")]
    public string Description { get; set; } = string.Empty;

    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
