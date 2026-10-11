namespace GroupAnnouncementApp.Helpers;

// People sign in with a phone number and a password. Firebase Auth still needs an email-shaped
// name, so the phone number (digits only) is turned into a private address that is never shown.
public static class PhoneLogin
{
    private const string EmailDomain = "phone.groupannouncement.app";

    // Keeps digits only, so "98765 43210" and "9876543210" are the same account.
    public static string Normalize(string phone)
    {
        string digits = string.Empty;
        for (int i = 0; i < phone.Length; i++)
        {
            char c = phone[i];
            if (c >= '0' && c <= '9')
            {
                digits = digits + c;
            }
        }

        return digits;
    }

    // The hidden Firebase sign-in name for a phone number.
    public static string ToEmail(string normalizedPhone)
    {
        return normalizedPhone + "@" + EmailDomain;
    }
}
