using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Phase 2. Firestore document: groupFeed/{groupId}
// Everything a member's Home card needs for ONE group, in one small document: the latest announcement
// (flattened into the "a..." fields) and the planned outage (the "o..." fields). The admin app writes it
// when it posts, edits, completes or deletes an announcement and when it schedules or cancels an outage.
// A member listens to the feed documents of the member's own groups, so a change reaches the phone at once
// and costs one read per listening member, instead of five reads per group every 20 seconds.
//
// The old collections (announcements, outages) stay the source of truth for the admin screens.
public sealed class GroupFeedDocument : IFirestoreObject
{
    private const string DefaultStatus = "Active";

    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("groupId")]
    public string GroupId { get; set; } = string.Empty;

    // Latest announcement. Empty id = the group has no active announcement (members see the 5-minute notice).
    [FirestoreProperty("aId")]
    public string AnnouncementId { get; set; } = string.Empty;

    [FirestoreProperty("aTitle")]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty("aMessage")]
    public string Message { get; set; } = string.Empty;

    [FirestoreProperty("aStatus")]
    public string Status { get; set; } = string.Empty;

    [FirestoreProperty("aResolveMinutes")]
    public int ResolveMinutes { get; set; }

    [FirestoreProperty("aCompletedAtMs")]
    public long CompletedAtMs { get; set; }

    [FirestoreProperty("aAttachmentName")]
    public string AttachmentName { get; set; } = string.Empty;

    [FirestoreProperty("aAttachmentKind")]
    public string AttachmentKind { get; set; } = string.Empty;

    // Phase 4: id of the shared file (see Announcement.SharedFileId). Empty = none / per-group copy.
    [FirestoreProperty("aFileId")]
    public string SharedFileId { get; set; } = string.Empty;

    [FirestoreProperty("aEdited")]
    public bool IsEdited { get; set; }

    // Server time of the post. Written only when the whole document is written (a post or a rebuild),
    // never by the field updates for edit / complete / outage.
    [FirestoreServerTimestamp("aAt")]
    public DateTimeOffset PostedAt { get; set; }

    // The original post time in Unix milliseconds. Only a rebuild (migration or repair) sets it, so an old
    // message keeps its real time; 0 = use PostedAt.
    [FirestoreProperty("aAtMs")]
    public long PostedAtMs { get; set; }

    // Planned outage. Empty status = none.
    [FirestoreProperty("oStatus")]
    public string OutageStatus { get; set; } = string.Empty;

    [FirestoreProperty("oStartMs")]
    public long OutageStartMs { get; set; }

    [FirestoreProperty("oEndMs")]
    public long OutageEndMs { get; set; }

    [FirestoreProperty("oPublishAtMs")]
    public long OutagePublishAtMs { get; set; }

    [FirestoreProperty("oCancelledAtMs")]
    public long OutageCancelledAtMs { get; set; }

    [FirestoreProperty("version")]
    public int Version { get; set; } = 1;

    public bool HasAnnouncement
    {
        get { return !string.IsNullOrEmpty(AnnouncementId); }
    }

    // The announcement as the existing card code expects it (null when there is none).
    public Announcement? ToAnnouncement(string groupId)
    {
        if (!HasAnnouncement)
        {
            return null;
        }

        Announcement announcement = new Announcement();
        announcement.Id = AnnouncementId;
        announcement.GroupId = groupId;
        announcement.Title = Title;
        announcement.Message = Message;
        announcement.Status = string.IsNullOrEmpty(Status) ? DefaultStatus : Status;
        announcement.ResolveMinutes = ResolveMinutes;
        announcement.CompletedAtMs = CompletedAtMs;
        announcement.AttachmentName = AttachmentName;
        announcement.AttachmentKind = AttachmentKind;
        announcement.SharedFileId = SharedFileId;
        announcement.IsActive = true;
        announcement.IsEdited = IsEdited;
        announcement.CreatedAt = PostedAtMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(PostedAtMs) : PostedAt;
        return announcement;
    }

    // The outage as the existing card code expects it (null when there is none).
    public Outage? ToOutage(string groupId)
    {
        if (string.IsNullOrEmpty(OutageStatus))
        {
            return null;
        }

        Outage outage = new Outage();
        outage.Id = "current";
        outage.GroupId = groupId;
        outage.Status = OutageStatus;
        outage.StartMs = OutageStartMs;
        outage.EndMs = OutageEndMs;
        outage.PublishAtMs = OutagePublishAtMs;
        outage.CancelledAtMs = OutageCancelledAtMs;
        return outage;
    }

    // The whole document for a group after a post. The outage that is already in the feed is carried over.
    public static GroupFeedDocument ForPost(string groupId, Announcement announcement, GroupFeedDocument? previous)
    {
        GroupFeedDocument feed = ForRebuild(groupId, announcement, null);
        if (previous != null)
        {
            feed.OutageStatus = previous.OutageStatus;
            feed.OutageStartMs = previous.OutageStartMs;
            feed.OutageEndMs = previous.OutageEndMs;
            feed.OutagePublishAtMs = previous.OutagePublishAtMs;
            feed.OutageCancelledAtMs = previous.OutageCancelledAtMs;
        }

        return feed;
    }

    // The whole document built from the old collections (migration and repair).
    public static GroupFeedDocument ForRebuild(string groupId, Announcement? announcement, Outage? outage)
    {
        GroupFeedDocument feed = new GroupFeedDocument();
        feed.GroupId = groupId;
        if (announcement != null)
        {
            feed.AnnouncementId = announcement.Id;
            feed.Title = announcement.Title;
            feed.Message = announcement.Message;
            feed.Status = announcement.Status;
            feed.ResolveMinutes = announcement.ResolveMinutes;
            feed.CompletedAtMs = announcement.CompletedAtMs;
            feed.AttachmentName = announcement.AttachmentName;
            feed.AttachmentKind = announcement.AttachmentKind;
            feed.SharedFileId = announcement.SharedFileId;
            feed.IsEdited = announcement.IsEdited;
            feed.PostedAtMs = announcement.CreatedAt == default ? 0 : announcement.CreatedAt.ToUnixTimeMilliseconds();
        }

        if (outage != null)
        {
            feed.OutageStatus = outage.Status;
            feed.OutageStartMs = outage.StartMs;
            feed.OutageEndMs = outage.EndMs;
            feed.OutagePublishAtMs = outage.PublishAtMs;
            feed.OutageCancelledAtMs = outage.CancelledAtMs;
        }

        return feed;
    }
}
