using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: adminRequests/{uid}. One open request per person; deleted once decided.
public sealed class AdminRequest : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    // The details the person filled in. Old requests (before the form existed) have these empty.
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

    // The optional ID card photo, stored as base64 text (at most 200 KB before the conversion).
    [FirestoreProperty("idCardName")]
    public string IdCardName { get; set; } = string.Empty;

    [FirestoreProperty("idCardContentType")]
    public string IdCardContentType { get; set; } = string.Empty;

    [FirestoreProperty("idCardData")]
    public string IdCardData { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
