namespace GroupAnnouncementApp.Helpers;

// Single place to change group limits.
// The name/description lengths are ALSO enforced in firestore.rules (60 and 300).
// If you change them here, change them in the rules too.
// There is no limit on the number of groups.
public static class GroupLimits
{
    public const int MaxNameLength = 60;

    public const int MaxDescriptionLength = 300;

    // The group number is a positive whole number (1, 2, 3 ...). The only limit is that it fits
    // in 18 digits. ALSO enforced in firestore.rules.
    public const int MaxNumberLength = 18;
}
