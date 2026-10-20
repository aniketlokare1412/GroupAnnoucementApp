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
}
