using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: adminProfiles/{uid}
// The service details of an admin (copied from the admin request when it is approved). They are shown with the
// messages the admin publishes. Only admins write it; the admin reads their own.
public sealed class AdminProfile : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("designation")]
    public string Designation { get; set; } = string.Empty;

    [FirestoreProperty("cpfNumber")]
    public string CpfNumber { get; set; } = string.Empty;

    [FirestoreProperty("division")]
    public string Division { get; set; } = string.Empty;

    [FirestoreProperty("subDivision")]
    public string SubDivision { get; set; } = string.Empty;

    [FirestoreProperty("section")]
    public string Section { get; set; } = string.Empty;

    [FirestoreProperty("subStationCode")]
    public string SubStationCode { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
