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

    // Each group has its own tile colour.
    public Color TileBackground { get; private set; } = TileColors.Background(null);
    public Color TileForeground { get; private set; } = TileColors.Foreground(null);

    // Tapping the row (or its bell) opens this group's announcements.
    public ICommand? OpenCommand { get; private set; }

    // Admin: reports made on the 5-minute notice, and the button that clears them once the admin has seen them.
    public int NoticePanicCount { get; private set; }

    public ICommand? ClearNoticeCommand { get; private set; }

    public bool HasNoticePanics
    {
        get { return NoticePanicCount > 0; }
    }

    public string NoticePanicText
    {
        get { return "5-minute notice reports: " + NoticePanicCount; }
    }

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

    public static HomeGroupItem FromGroup(AnnouncementGroup group, Func<HomeGroupItem, Task> onOpen, int panicCount = 0, int noticePanicCount = 0, Func<HomeGroupItem, Task>? onClearNotice = null)
    {
        HomeGroupItem item = new HomeGroupItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = GroupText.WithNumber(group, panicCount);
        item.IsActive = group.IsActive;
        item.Initials = InitialsHelper.From(group.Name);
        item.TileBackground = TileColors.Background(group.Name);
        item.TileForeground = TileColors.Foreground(group.Name);
        item.OpenCommand = new AsyncRelayCommand(() => onOpen(item));
        item.NoticePanicCount = noticePanicCount;
        if (onClearNotice != null)
        {
            item.ClearNoticeCommand = new AsyncRelayCommand(() => onClearNotice(item));
        }

        return item;
    }
}
