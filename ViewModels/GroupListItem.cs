using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one row of the groups list shows. Rebuilt whenever the list is reloaded.
public sealed class GroupListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public bool IsInactive
    {
        get { return !IsActive; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    public static GroupListItem FromGroup(AnnouncementGroup group)
    {
        GroupListItem item = new GroupListItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.IsActive = group.IsActive;
        return item;
    }
}
