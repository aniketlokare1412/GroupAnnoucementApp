using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Helpers;

// "Published by ..." under a message: the admin's name and a short line of details.
// CPF number is not shown to the members.
public static class AdminAuthorText
{
    public const int MaxNameLength = 100;
    public const int MaxDetailsLength = 300;

    // e.g. "AE | Section: Pune East | Sub Station: SS12". Empty when the admin has no details saved.
    public static string Details(AdminProfile? profile)
    {
        if (profile == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Designation))
        {
            parts.Add(profile.Designation.Trim());
        }

        if (!string.IsNullOrWhiteSpace(profile.Section))
        {
            parts.Add("Section: " + profile.Section.Trim());
        }

        if (!string.IsNullOrWhiteSpace(profile.SubStationCode))
        {
            parts.Add("Sub Station: " + profile.SubStationCode.Trim());
        }

        string text = string.Join(" | ", parts);
        return text.Length > MaxDetailsLength ? text.Substring(0, MaxDetailsLength) : text;
    }

    public static string ByLine(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : "Published by " + name.Trim();
    }
}
