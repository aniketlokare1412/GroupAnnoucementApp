namespace GroupAnnouncementApp.Helpers;

// Single place to change announcement limits.
// The title/message lengths are ALSO enforced in firestore.rules (100 and 2000).
// If you change them here, change them in the rules too.
public static class AnnouncementLimits
{
    public const int MaxTitleLength = 100;

    public const int MaxMessageLength = 2000;

    // How many announcements the list shows at a time ("Load more" shows the next batch).
    public const int PageSize = 20;
}
