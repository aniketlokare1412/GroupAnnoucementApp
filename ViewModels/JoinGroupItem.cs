using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one row of the "Join a group" list shows.
public sealed class JoinGroupItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsMember { get; private set; }

    public bool IsNotMember
    {
        get { return !IsMember; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    public static JoinGroupItem FromJoinable(JoinableGroup joinable)
    {
        JoinGroupItem item = new JoinGroupItem();
        item.Id = joinable.Group.Id;
        item.Name = joinable.Group.Name;
        item.Description = joinable.Group.Description;
        item.IsMember = joinable.IsMember;
        return item;
    }
}
