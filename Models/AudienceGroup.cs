namespace GroupAnnouncementApp.Models;

// A group the admin can pick as an audience (an announcement or an outage). Only active groups are passed in.
public sealed class AudienceGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
}

// What the picker page is opened with: the groups to choose from and the ones already ticked.
public sealed class AudienceRequest
{
    public List<AudienceGroup> Groups { get; set; } = new List<AudienceGroup>();
    public HashSet<string> Selected { get; set; } = new HashSet<string>();

    // The tab to open on: 0 = Groups, 1 = Communities.
    public int Segment { get; set; }

    // For the Communities tab: the super community and communities to show ticked again (null = start empty).
    public AudienceHierarchyState? Hierarchy { get; set; }
}

// What the Communities tab of the picker had chosen when the admin tapped Done, so reopening the picker with the same
// groups shows that tab again (the group ids alone cannot tell which communities they came from).
public sealed class AudienceHierarchyState
{
    public string SuperCommunityId { get; set; } = string.Empty;
    public HashSet<string> CommunityIds { get; set; } = new HashSet<string>();
    public HashSet<string> GroupIds { get; set; } = new HashSet<string>();
}
