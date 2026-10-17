namespace GroupAnnouncementApp.Helpers;

public static class ChipText
{
    // The first few names as small chips, then one "+N more" chip. Used on the admin list cards,
    // so no edit screen is needed to see what belongs where.
    public static List<string> Build(List<string> names, int max)
    {
        List<string> sorted = new List<string>(names);
        sorted.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

        List<string> chips = new List<string>();
        for (int i = 0; i < sorted.Count && i < max; i++)
        {
            chips.Add(sorted[i]);
        }

        if (sorted.Count > max)
        {
            chips.Add("+" + (sorted.Count - max) + " more");
        }

        return chips;
    }
}
