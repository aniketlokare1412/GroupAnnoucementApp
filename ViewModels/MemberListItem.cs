using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one row of the admin's member list shows.
public sealed class MemberListItem
{
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string StatusText { get; private set; } = string.Empty;

    public bool HasStatus
    {
        get { return StatusText.Length > 0; }
    }

    public static MemberListItem FromEntry(GroupMemberEntry entry)
    {
        MemberListItem item = new MemberListItem();
        item.UserId = entry.UserId;
        item.Name = entry.Name;
        item.Email = entry.Email;

        if (entry.ProfileMissing)
        {
            item.StatusText = "Profile missing";
        }
        else if (!entry.IsUserActive)
        {
            item.StatusText = "Deactivated user";
        }
        else if (entry.IsAdmin)
        {
            item.StatusText = "Administrator";
        }

        return item;
    }
}
