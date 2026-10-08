using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one card of the admin's member list shows. Rebuilt whenever the list is reloaded.
public sealed class MemberListItem
{
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;

    // At most one of these is true (same order as before: missing, deactivated, administrator).
    public bool IsProfileMissing { get; private set; }
    public bool IsDeactivated { get; private set; }
    public bool IsAdministrator { get; private set; }

    // The close (x) button on the card.
    public ICommand? RemoveCommand { get; private set; }

    public bool HasEmail
    {
        get { return !string.IsNullOrWhiteSpace(Email); }
    }

    // A deactivated user or a missing profile gets the grey initials tile.
    public bool IsNormal
    {
        get { return !IsProfileMissing && !IsDeactivated; }
    }

    public bool IsMuted
    {
        get { return IsProfileMissing || IsDeactivated; }
    }

    // Read by screen readers for the icon-only remove button.
    public string RemoveLabel
    {
        get { return "Remove " + Name + " from this group"; }
    }

    public static MemberListItem FromEntry(GroupMemberEntry entry, Func<MemberListItem, Task> onRemove)
    {
        MemberListItem item = new MemberListItem();
        item.UserId = entry.UserId;
        item.Name = entry.Name;
        item.Email = entry.Email;
        item.Initials = InitialsHelper.From(string.IsNullOrWhiteSpace(entry.Name) ? entry.Email : entry.Name);
        item.RemoveCommand = new AsyncRelayCommand(() => onRemove(item));

        if (entry.ProfileMissing)
        {
            item.IsProfileMissing = true;
        }
        else if (!entry.IsUserActive)
        {
            item.IsDeactivated = true;
        }
        else if (entry.IsAdmin)
        {
            item.IsAdministrator = true;
        }

        return item;
    }
}
