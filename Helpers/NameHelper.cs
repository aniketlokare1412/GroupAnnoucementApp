namespace GroupAnnouncementApp.Helpers;

public static class NameHelper
{
    // True when the "name" is really a phone number (digits, plus, spaces, dashes; at least 7 digits).
    public static bool LooksLikePhone(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int digits = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsDigit(c))
            {
                digits++;
            }
            else if (c != '+' && c != ' ' && c != '-')
            {
                return false;
            }
        }

        return digits >= 7;
    }
}
