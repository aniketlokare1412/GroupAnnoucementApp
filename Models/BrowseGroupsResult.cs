namespace GroupAnnouncementApp.Models;

// The result of loading the "Join a group" list.
public sealed class BrowseGroupsResult
{
    // Every ACTIVE group, sorted by name.
    public List<JoinableGroup> Groups { get; set; } = new List<JoinableGroup>();

    // False when the groups the user already joined could not be looked up.
    // The list is still usable: searching and joining work, but "Joined" labels may be missing.
    public bool MembershipKnown { get; set; } = true;
}
