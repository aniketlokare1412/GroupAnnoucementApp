namespace GroupAnnouncementApp.Helpers;

// Limits for communities and super communities. Name and description match the groups.
// ALSO enforced in firestore.rules. If you change a value here, change it there too.
public static class CommunityLimits
{
    public const int MaxNameLength = 60;

    public const int MaxDescriptionLength = 300;

    // A positive whole number without a leading zero, at most 12 digits.
    public const int MaxNumberLength = 12;
}
