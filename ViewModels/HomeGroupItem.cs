using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One group row on Home (admin preview and member "My groups"). Rebuilt whenever the list is reloaded.
public sealed class HomeGroupItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // Tapping the row (or its bell) opens this group's announcements.
    public ICommand? OpenCommand { get; private set; }

    public bool IsInactive
    {
        get { return !IsActive; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    // Read by screen readers for the icon-only bell button.
    public string AnnouncementsLabel
    {
        get { return "Announcements for " + Name; }
    }

    public static HomeGroupItem FromGroup(AnnouncementGroup group, Func<HomeGroupItem, Task> onOpen)
    {
        HomeGroupItem item = new HomeGroupItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.IsActive = group.IsActive;
        item.Initials = InitialsHelper.From(group.Name);
        item.OpenCommand = new AsyncRelayCommand(() => onOpen(item));
        return item;
    }
}
