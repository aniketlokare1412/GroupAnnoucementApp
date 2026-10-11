using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Helpers;

public static class GroupText
{
    // "No. 1234" (or "No number yet"), followed by the description when there is one.
    public static string WithNumber(AnnouncementGroup group)
    {
        string number = string.IsNullOrWhiteSpace(group.Number) ? "No number yet" : "No. " + group.Number;
        if (string.IsNullOrWhiteSpace(group.Description))
        {
            return number;
        }

        return number + " - " + group.Description;
    }
}
