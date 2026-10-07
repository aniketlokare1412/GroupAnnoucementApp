namespace GroupAnnouncementApp.Helpers;

// Single place to change group limits.
// The name/description lengths are ALSO enforced in firestore.rules (60 and 300).
// If you change them here, change them in the rules too.
// MaxActiveGroups is only enforced in the app (rules cannot count documents).
public static class GroupLimits
{
    public const int MaxActiveGroups = 200;

    public const int MaxNameLength = 60;

    public const int MaxDescriptionLength = 300;
}
