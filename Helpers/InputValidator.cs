namespace GroupAnnouncementApp.Helpers;

public static class InputValidator
{
    public static bool IsValidEmail(string email)
    {
        if (email.IndexOf(' ') >= 0)
        {
            return false;
        }

        int at = email.IndexOf('@');
        if (at <= 0)
        {
            return false;
        }

        if (at != email.LastIndexOf('@'))
        {
            return false;
        }

        int dot = email.LastIndexOf('.');
        if (dot < at + 2)
        {
            return false;
        }

        if (dot >= email.Length - 1)
        {
            return false;
        }

        return true;
    }

    // Allows digits plus + - ( ) and spaces. Needs 7 to 15 digits.
    public static bool IsValidPhone(string phone)
    {
        int digitCount = 0;

        for (int i = 0; i < phone.Length; i++)
        {
            char c = phone[i];

            if (c >= '0' && c <= '9')
            {
                digitCount++;
            }
            else if (c == '+' || c == '-' || c == '(' || c == ')' || c == ' ')
            {
                // allowed formatting character
            }
            else
            {
                return false;
            }
        }

        return digitCount >= 7 && digitCount <= 15;
    }
}
