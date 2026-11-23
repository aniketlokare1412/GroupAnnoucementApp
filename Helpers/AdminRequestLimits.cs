using System.Globalization;

namespace GroupAnnouncementApp.Helpers;

// The details a member fills in before an admin request can be sent.
public static class AdminRequestLimits
{
    public const int MaxTextLength = 100;

    // The designation drop-down: the person picks exactly one.
    public static readonly string[] Designations = new string[]
    {
        "AE",
        "Chief Technician",
        "Sr. Technician",
        "Technician",
        "Out Source"
    };

    // ID card photo: JPEG or PNG only.
    public static readonly FilePickerFileType IdCardPickerTypes = new FilePickerFileType(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, new string[] { "image/jpeg", "image/png" } },
            { DevicePlatform.iOS, new string[] { "public.jpeg", "public.png" } },
            { DevicePlatform.MacCatalyst, new string[] { "public.jpeg", "public.png" } },
            { DevicePlatform.WinUI, new string[] { ".jpg", ".jpeg", ".png" } }
        });

    // The only check for now: no special characters. Letters (any language), digits and spaces are fine.
    public static bool IsAllowedChar(char c)
    {
        if (char.IsLetterOrDigit(c) || c == ' ')
        {
            return true;
        }

        // Marathi and other scripts use combining marks (matras) that are not "letters".
        UnicodeCategory category = char.GetUnicodeCategory(c);
        return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark;
    }

    // Returns a message for the first problem, or null.
    public static string? CheckText(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Enter " + label + ".";
        }

        if (value.Length > MaxTextLength)
        {
            return label + " can be up to " + MaxTextLength + " characters.";
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (!IsAllowedChar(value[i]))
            {
                return label + " can only have letters, numbers and spaces.";
            }
        }

        return null;
    }

    public static bool IsValidDesignation(string value)
    {
        for (int i = 0; i < Designations.Length; i++)
        {
            if (Designations[i] == value)
            {
                return true;
            }
        }

        return false;
    }
}
