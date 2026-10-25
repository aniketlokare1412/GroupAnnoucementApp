using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Helpers;

// When a member stops seeing an announcement that the wireman never marked as completed.
// The standing 5-minute notice (with its panic button) takes its place. Admins are not affected.
public static class MessageExpiry
{
    // A message with a resolution time disappears this long after that time is up.
    public const int MinutesAfterTimeUp = 5;

    // A message with no resolution time disappears this long after it was posted.
    public const int MinutesWhenNoTime = 10;

    public static bool IsExpired(Announcement announcement, DateTimeOffset now)
    {
        if (announcement.Status == AnnouncementStatus.Completed)
        {
            return false;
        }

        // A message just posted can still have no server time on this phone: it is brand new.
        if (announcement.CreatedAt == default)
        {
            return false;
        }

        int minutes = announcement.ResolveMinutes > 0
            ? announcement.ResolveMinutes + MinutesAfterTimeUp
            : MinutesWhenNoTime;
        return now >= announcement.CreatedAt.AddMinutes(minutes);
    }
}
