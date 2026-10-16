using System.Globalization;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Helpers;

// The outage messages (edit the wording here) and the rule for when they are shown.
public static class OutageText
{
    public const string Scheduled = "Scheduled";

    public const string Cancelled = "Cancelled";

    // Shown from the publish time until the outage is over (a cancelled outage is shown the same way).
    public static bool IsShownAt(Outage outage, DateTimeOffset now)
    {
        long nowMs = now.ToUnixTimeMilliseconds();
        return nowMs >= outage.PublishAtMs && nowMs < outage.EndMs;
    }

    // "उद्या दि. 19/10/2026 सकाळी 10:00 ते 17:00 मेनटेनन्स व इतर कामासाठी लाईट बंद राहील. ..."
    public static string BuildPlanned(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        DateTimeOffset localStart = start.ToLocalTime();
        return DayWord(localStart, now.ToLocalTime())
            + "दि. " + DateText(localStart) + " " + PartOfDay(localStart) + " "
            + TimeText(localStart) + " ते " + TimeText(end.ToLocalTime())
            + " मेनटेनन्स व इतर कामासाठी लाईट बंद राहील. आपल्याला होणाऱ्या त्रासाची आम्हाला कल्पना आहे. सहकार्य करावे.";
    }

    // "नियमित काम रद्द झाले असून ... दि. 19/10/2026 सकाळी 10:00 ते 17:00, If you have any questions please ask."
    public static string BuildCancelled(DateTimeOffset start, DateTimeOffset end)
    {
        DateTimeOffset localStart = start.ToLocalTime();
        return "नियमित काम रद्द झाले असून लाईट नियमित चालू ठेवण्यात येणार आहे. दि. "
            + DateText(localStart) + " " + PartOfDay(localStart) + " "
            + TimeText(localStart) + " ते " + TimeText(end.ToLocalTime())
            + ", If you have any questions please ask.";
    }

    // "आज " / "उद्या " / "परवा " when the outage date is today, tomorrow or the day after.
    private static string DayWord(DateTimeOffset localStart, DateTimeOffset localNow)
    {
        int days = (localStart.Date - localNow.Date).Days;
        if (days == 0)
        {
            return "आज ";
        }

        if (days == 1)
        {
            return "उद्या ";
        }

        if (days == 2)
        {
            return "परवा ";
        }

        return string.Empty;
    }

    // From the start time: morning, afternoon, evening or night.
    private static string PartOfDay(DateTimeOffset localStart)
    {
        int hour = localStart.Hour;
        if (hour < 12)
        {
            return "सकाळी";
        }

        if (hour < 16)
        {
            return "दुपारी";
        }

        if (hour < 20)
        {
            return "संध्याकाळी";
        }

        return "रात्री";
    }

    private static string DateText(DateTimeOffset local)
    {
        return local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static string TimeText(DateTimeOffset local)
    {
        return local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
