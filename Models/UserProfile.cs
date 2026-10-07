using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: users/{uid}
public sealed class UserProfile : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("userType")]
    public string UserType { get; set; } = UserTypes.Regular;

    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; } = true;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
