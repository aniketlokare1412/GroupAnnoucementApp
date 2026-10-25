using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore documents: directory/main and directory/admin (see DirectorySnapshot for what they hold).
// Both have the same shape: the list as JSON text, a format version, and the server time of the last rebuild.
public sealed class DirectoryDocument : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("data")]
    public string Data { get; set; } = string.Empty;

    [FirestoreProperty("version")]
    public int Version { get; set; } = 1;

    [FirestoreServerTimestamp("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
