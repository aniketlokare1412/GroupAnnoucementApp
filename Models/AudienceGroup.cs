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
}
