namespace GroupAnnouncementApp.Models;

// What the admin Home needs to order the groups. Not a Firestore document.
public sealed class GroupActivity
{
    // Panic presses on the group's newest announcement.
    public int PanicCount { get; set; }

    // When the admin last posted to the group (default = never).
    public DateTimeOffset LastPostedAt { get; set; }

    // The newest announcement is marked completed (its panic presses no longer need attention).
    public bool IsCompleted { get; set; }

    // The raw counters of the stats document (Phase 4: posting skips the panic clean-up when they are 0).
    public bool HasAnnouncement { get; set; }
    public int AnnouncementPanicCount { get; set; }
    public int NoticePanicCount { get; set; }
}
