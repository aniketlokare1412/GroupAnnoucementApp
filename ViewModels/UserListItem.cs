using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one row of the users list shows. Rebuilt whenever the list is reloaded.
public sealed class UserListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsSelf { get; private set; }

    public bool IsDeactivated
    {
        get { return !IsActive; }
    }

    public string NameText
    {
        get
        {
            if (IsSelf)
            {
                return Name + " (you)";
            }
            return Name;
        }
    }

    public string RoleText
    {
        get
        {
            if (IsAdmin)
            {
                return "Administrator";
            }
            return "Member";
        }
    }

    public static UserListItem FromProfile(UserProfile profile, string? currentUid)
    {
        UserListItem item = new UserListItem();
        item.Id = profile.Id;
        item.Email = profile.Email;
        item.Phone = profile.Phone;
        item.IsActive = profile.IsActive;
        item.IsAdmin = string.Equals(profile.UserType, UserTypes.Admin, StringComparison.Ordinal);
        item.IsSelf = currentUid != null && profile.Id == currentUid;

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            item.Name = profile.Email;
        }
        else
        {
            item.Name = profile.Name;
        }

        return item;
    }
}
