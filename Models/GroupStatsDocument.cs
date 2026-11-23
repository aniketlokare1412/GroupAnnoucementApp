using Plugin.Firebase.Firestore;

namespace GroupAnnouncementApp.Models;

// Phase 3. Firestore document: groupStats/{groupId}
// What the ADMIN screens need to order and badge a group, in one small document: how many panic reports the
// open message (or the 5-minute notice) has, when the admin last posted, and whether the work is completed.
// Admin Home and the Groups tab read / listen to these documents instead of querying every group's
// announcements and counting every panic press.
//
// Written by the admin app when it posts, completes or deletes an announcement; a member only ever adds 1 to
// panicCount or noticePanicCount (the rules check that). Rebuilt from the real data by "Rebuild stats".
public sealed class GroupStatsDocument : IFirestoreObject
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    // Panic presses on the open announcement (only meaningful while HasAnnouncement and not IsCompleted).
    [FirestoreProperty("panicCount")]
    public int PanicCount { get; set; }

    // Panic presses on the 5-minute notice (shown when the group has no open announcement, or it is completed).
    [FirestoreProperty("noticePanicCount")]
    public int NoticePanicCount { get; set; }

    // When the admin last posted to the group, Unix milliseconds. 0 = never / the message was deleted.
    [FirestoreProperty("lastPostedAtMs")]
    public long LastPostedAtMs { get; set; }

    [FirestoreProperty("isCompleted")]
    public bool IsCompleted { get; set; }

    [FirestoreProperty("hasAnnouncement")]
    public bool HasAnnouncement { get; set; }

    [FirestoreProperty("version")]
    public int Version { get; set; } = 1;

    // The reports that need attention. An open message counts its own reports PLUS the reports on the 5-minute notice:
    // once a message has expired the members see the notice instead, and their presses are saved on the notice
    // (posting a new message clears them). A completed message or no message: the notice reports only.
    public int OpenPanics
    {
        get { return HasAnnouncement && !IsCompleted ? PanicCount + NoticePanicCount : NoticePanicCount; }
    }

    public GroupActivity ToActivity()
    {
        GroupActivity activity = new GroupActivity();
        activity.PanicCount = OpenPanics;
        activity.LastPostedAt = LastPostedAtMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(LastPostedAtMs) : default;
        activity.IsCompleted = HasAnnouncement && IsCompleted;
        activity.HasAnnouncement = HasAnnouncement;
        activity.AnnouncementPanicCount = PanicCount;
        activity.NoticePanicCount = NoticePanicCount;
        return activity;
    }

    // A fresh post: the new message has no reports and the notice reports are cleared.
    public static GroupStatsDocument ForPost(long postedAtMs)
    {
        GroupStatsDocument stats = new GroupStatsDocument();
        stats.HasAnnouncement = true;
        stats.LastPostedAtMs = postedAtMs;
        return stats;
    }
}
