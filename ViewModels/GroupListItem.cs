using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one card of the admin Groups list shows. Rebuilt whenever the list is reloaded.
// The icon buttons are commands that live on the card itself, so the page needs no cross-binding.
public sealed class GroupListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // Tap on the card and the bell: this group's announcements.
    public ICommand? OpenCommand { get; private set; }
    public ICommand? MembersCommand { get; private set; }
    public ICommand? EditCommand { get; private set; }

    // Activate or deactivate (the page shows the matching icon button).
    public ICommand? ToggleActiveCommand { get; private set; }

    public bool IsInactive
    {
        get { return !IsActive; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    // Read by screen readers for the icon-only buttons.
    public string AnnouncementsLabel
    {
        get { return "Announcements for " + Name; }
    }

    public string MembersLabel
    {
        get { return "Members of " + Name; }
    }

    public string EditLabel
    {
        get { return "Edit " + Name; }
    }

    public string DeactivateLabel
    {
        get { return "Deactivate " + Name; }
    }

    public string ActivateLabel
    {
        get { return "Activate " + Name; }
    }

    public string CardLabel
    {
        get { return IsActive ? Name : Name + ", inactive"; }
    }

    // Plain version without icon-button commands. The old "pick a group" page
    // (AnnouncementGroupsViewModel, no longer reachable from the UI) still uses it.
    public static GroupListItem FromGroup(AnnouncementGroup group)
    {
        GroupListItem item = new GroupListItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.IsActive = group.IsActive;
        item.Initials = InitialsHelper.From(group.Name);
        return item;
    }

    public static GroupListItem FromGroup(
        AnnouncementGroup group,
        Func<GroupListItem, Task> onOpen,
        Func<GroupListItem, Task> onMembers,
        Func<GroupListItem, Task> onEdit,
        Func<GroupListItem, Task> onToggleActive)
    {
        GroupListItem item = new GroupListItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.IsActive = group.IsActive;
        item.Initials = InitialsHelper.From(group.Name);
        item.OpenCommand = new AsyncRelayCommand(() => onOpen(item));
        item.MembersCommand = new AsyncRelayCommand(() => onMembers(item));
        item.EditCommand = new AsyncRelayCommand(() => onEdit(item));
        item.ToggleActiveCommand = new AsyncRelayCommand(() => onToggleActive(item));
        return item;
    }
}
