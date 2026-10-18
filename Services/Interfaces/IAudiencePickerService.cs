using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Opens the full-screen audience picker and hands the result back to the page that asked.
// Used by the announcement composer and the outage page, so both choose groups the same way.
public interface IAudiencePickerService
{
    // What the picker page should show. Null when nothing is being picked.
    AudienceRequest? Pending { get; }

    // Opens the picker. Returns the ticked group ids when the admin taps Done,
    // or null when the admin goes back without confirming (the old selection stays).
    Task<HashSet<string>?> PickAsync(List<AudienceGroup> groups, HashSet<string> selected);

    // Called by the picker page. Safe to call more than once.
    void Complete(HashSet<string>? result);
}
