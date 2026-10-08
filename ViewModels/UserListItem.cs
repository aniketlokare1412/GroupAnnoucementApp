using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one card of the admin Users list shows. Rebuilt whenever the list is reloaded.
// The icon buttons are commands that live on the card itself, so the page needs no cross-binding.
public sealed class UserListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool IsSelf { get; private set; }

    // Make admin / remove admin rights, and activate / deactivate.
    // The page shows the matching icon button (see the Can... flags).
    public ICommand? ToggleAdminCommand { get; private set; }
    public ICommand? ToggleActiveCommand { get; private set; }

    public bool IsDeactivated
    {
        get { return !IsActive; }
    }

    public bool HasPhone
    {
        get { return !string.IsNullOrWhiteSpace(Phone); }
    }

    // Admins cannot change their own role or status, so their own card has no action buttons.
    public bool CanManage
    {
        get { return !IsSelf; }
    }

    public bool CanMakeAdmin
    {
        get { return !IsSelf && !IsAdmin; }
    }

    public bool CanRemoveAdmin
    {
        get { return !IsSelf && IsAdmin; }
    }

    public bool CanDeactivate
    {
        get { return !IsSelf && IsActive; }
    }

    public bool CanActivate
    {
        get { return !IsSelf && !IsActive; }
    }

    // Read by screen readers.
    public string CardLabel
    {
        get
        {
            string text = Name;
            if (IsSelf)
            {
                text = text + ", you";
            }

            text = text + (IsAdmin ? ", administrator" : ", member");

            if (!IsActive)
            {
                text = text + ", deactivated";
            }

            return text;
        }
    }

    public string MakeAdminLabel
    {
        get { return "Make " + Name + " an administrator"; }
    }

    public string RemoveAdminLabel
    {
        get { return "Remove admin rights from " + Name; }
    }

    public string DeactivateLabel
    {
        get { return "Deactivate " + Name; }
    }

    public string ActivateLabel
    {
        get { return "Activate " + Name; }
    }

    public static UserListItem FromProfile(
        UserProfile profile,
        string? currentUid,
        Func<UserListItem, Task> onToggleAdmin,
        Func<UserListItem, Task> onToggleActive)
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

        item.Initials = InitialsHelper.From(item.Name);
        item.ToggleAdminCommand = new AsyncRelayCommand(() => onToggleAdmin(item));
        item.ToggleActiveCommand = new AsyncRelayCommand(() => onToggleActive(item));
        return item;
    }
}
