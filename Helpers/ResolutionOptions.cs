namespace GroupAnnouncementApp.Helpers;

// The "Resolution time" drop-down: how long after posting the work should be done.
public static class ResolutionOptions
{
    private static readonly string[] LabelItems = new string[]
    {
        "Not set", "10 minutes", "15 minutes", "20 minutes", "30 minutes", "45 minutes",
        "1 hour", "1 hour 30 min", "2 hours", "3 hours", "4 hours", "6 hours", "12 hours",
        "Custom (HH:MM)"
    };

    private static readonly int[] MinuteItems = new int[] { 0, 10, 15, 20, 30, 45, 60, 90, 120, 180, 240, 360, 720, -1 };

    // The longest allowed resolution time (7 days). ALSO enforced in firestore.rules.
    public const int MaxMinutes = 10080;

    public static int CustomIndex
    {
        get { return LabelItems.Length - 1; }
    }

    public static List<string> Labels()
    {
        return new List<string>(LabelItems);
    }

    // Minutes for a ready-made choice (0 = not set). Not for the custom choice.
    public static int MinutesAt(int index)
    {
        if (index < 0 || index >= MinuteItems.Length || index == CustomIndex)
        {
            return 0;
        }

        return MinuteItems[index];
    }

    // "01:15" -> 75. Hours 0-168, minutes 0-59, at least one minute.
    public static bool TryParseHoursMinutes(string text, out int minutes)
    {
        minutes = 0;
        string[] parts = (text ?? string.Empty).Trim().Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        int hours;
        int mins;
        if (!int.TryParse(parts[0], out hours) || !int.TryParse(parts[1], out mins))
        {
            return false;
        }

        if (hours < 0 || mins < 0 || mins > 59)
        {
            return false;
        }

        int total = hours * 60 + mins;
        if (total <= 0 || total > MaxMinutes)
        {
            return false;
        }

        minutes = total;
        return true;
    }
}
