namespace GroupAnnouncementApp.Helpers;

public static class InitialsHelper
{
    // "Aniket Lokare" -> "AL", "Dev team" -> "DT", "Design" -> "DE", "" -> "?"
    public static string From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "?";
        }

        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            string word = parts[0];
            string firstTwo = word.Length >= 2 ? word.Substring(0, 2) : word;
            return firstTwo.ToUpperInvariant();
        }

        string first = parts[0].Substring(0, 1);
        string second = parts[1].Substring(0, 1);
        return (first + second).ToUpperInvariant();
    }
}
