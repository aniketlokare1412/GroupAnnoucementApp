using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Opens the full-screen audience picker and hands the result back to the page that asked.
// Used by the announcement composer and the outage page, so both choose groups the same way.
public interface IAudiencePickerService
{
    // What the picker page should show. Null when nothing is being picked.
    AudienceRequest? Pending { get; }

    // Opens the picker on the given tab (0 = Groups, 1 = Communities). `selected` are the groups ticked on the Groups tab;
    // `hierarchy` is what the Communities tab should show again (or null). Returns the chosen group ids when the admin
    // taps Done, or null when the admin goes back without confirming (the old choice stays).
    Task<HashSet<string>?> PickAsync(List<AudienceGroup> groups, HashSet<string> selected, int segment, AudienceHierarchyState? hierarchy);

    // The Communities tab choice of the last Done (null when the last Done was made in the Groups tab).
    AudienceHierarchyState? LastHierarchy { get; }

    void RememberHierarchy(AudienceHierarchyState? state);

    // The first step of New announcement / New outage: opens the page at `route` straight away.
    // The page itself has the "who is this for" section (Groups / Communities) that opens the picker.
    Task StartAsync(string route);

    // Called by the picker page. Safe to call more than once.
    void Complete(HashSet<string>? result);
}
