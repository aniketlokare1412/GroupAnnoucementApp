namespace GroupAnnouncementApp.Helpers;

// Gives every group its own tile colour (picked from the group name, so it never changes).
public static class TileColors
{
    // blue, violet, pink, orange, teal, yellow, sky
    private static readonly string[] LightBg = { "#DBEAFE", "#EDE9FE", "#FCE7F3", "#FFEDD5", "#CCFBF1", "#FEF9C3", "#E0F2FE" };
    private static readonly string[] LightFg = { "#1D4ED8", "#6D28D9", "#BE185D", "#C2410C", "#0F766E", "#A16207", "#0369A1" };
    private static readonly string[] DarkBg = { "#2A4CA8", "#4B3290", "#82285A", "#8A3F14", "#17615B", "#7A6208", "#14618F" };
    private static readonly string[] DarkFg = { "#BFDBFE", "#DDD6FE", "#FBCFE8", "#FED7AA", "#99F6E4", "#FEF08A", "#BAE6FD" };

    public static Color Background(string? key)
    {
        int i = IndexFor(key);
        return Color.FromArgb(IsDark() ? DarkBg[i] : LightBg[i]);
    }

    public static Color Foreground(string? key)
    {
        int i = IndexFor(key);
        return Color.FromArgb(IsDark() ? DarkFg[i] : LightFg[i]);
    }

    private static bool IsDark()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark;
    }

    private static int IndexFor(string? key)
    {
        string text = (key ?? string.Empty).Trim().ToLowerInvariant();
        int hash = 17;
        for (int i = 0; i < text.Length; i++)
        {
            hash = unchecked(hash * 31 + text[i]);
        }

        return (hash & 0x7FFFFFFF) % LightBg.Length;
    }
}
