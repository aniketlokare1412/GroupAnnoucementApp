namespace GroupAnnouncementApp.Models;

// An active group together with whether the signed-in user is already a member.
public sealed class JoinableGroup
{
    public AnnouncementGroup Group { get; set; } = new AnnouncementGroup();

    public bool IsMember { get; set; }
}
