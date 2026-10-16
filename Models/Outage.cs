using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Firestore document: groups/{groupId}/outages/current
// One planned outage per group. Times are Unix milliseconds. The message text is built on the phone
// when it is shown (see OutageText), so words like "tomorrow" are always right.
public sealed class Outage : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("groupId")]
    public string GroupId { get; set; } = string.Empty;

    // "Scheduled" or "Cancelled".
    [FirestoreProperty("status")]
    public string Status { get; set; } = string.Empty;

    // The outage itself: from and to.
    [FirestoreProperty("startMs")]
    public long StartMs { get; set; }

    [FirestoreProperty("endMs")]
    public long EndMs { get; set; }

    // Members see the outage message from this moment until the outage is over.
    [FirestoreProperty("publishAtMs")]
    public long PublishAtMs { get; set; }

    [FirestoreProperty("cancelledAtMs")]
    public long CancelledAtMs { get; set; }

    [FirestoreProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    // Written by the Firestore server, never by the device clock.
    [FirestoreServerTimestamp("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
