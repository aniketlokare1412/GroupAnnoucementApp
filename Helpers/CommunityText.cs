namespace GroupAnnouncementApp.Helpers;

public static class CommunityText
{
    // "No. 12", or "No number yet".
    public static string NumberText(string? number)
    {
        return string.IsNullOrWhiteSpace(number) ? "No number yet" : "No. " + number;
    }

    // "1 group", "3 groups".
    public static string Count(int count, string singular, string plural)
    {
        return count == 1 ? "1 " + singular : count + " " + plural;
    }
}
