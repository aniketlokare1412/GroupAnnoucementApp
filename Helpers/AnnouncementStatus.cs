namespace GroupAnnouncementApp.Helpers;

public static class AnnouncementStatus
{
    // Posted and the work is going on.
    public const string Active = "Active";

    // The admin marked the work as completed (the announcement updates in place, no re-post).
    public const string Completed = "Completed";
}
