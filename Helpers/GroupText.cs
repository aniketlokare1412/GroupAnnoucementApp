using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Helpers;

public static class GroupText
{
    // "No. 1234" (or "No number yet"), followed by the description when there is one.
    public static string WithNumber(AnnouncementGroup group, int panicCount = 0)
    {
        string number = string.IsNullOrWhiteSpace(group.Number) ? "No number yet" : "No. " + group.Number;
        string text = number;
        if (!string.IsNullOrWhiteSpace(group.Description))
        {
            text = text + " - " + group.Description;
        }

        if (panicCount > 0)
        {
            text = text + " - Panic reports: " + panicCount;
        }

        return text;
    }
}
