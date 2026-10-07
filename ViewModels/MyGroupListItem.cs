using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One row of "My groups": the group plus its two buttons (Announcements and Leave group).
public sealed class MyGroupListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public ICommand? OpenAnnouncementsCommand { get; private set; }
    public ICommand? LeaveCommand { get; private set; }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    public static MyGroupListItem FromGroup(
        AnnouncementGroup group,
        Func<MyGroupListItem, Task> onOpen,
        Func<MyGroupListItem, Task> onLeave)
    {
        MyGroupListItem item = new MyGroupListItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.OpenAnnouncementsCommand = new AsyncRelayCommand(() => onOpen(item));
        item.LeaveCommand = new AsyncRelayCommand(() => onLeave(item));
        return item;
    }
}
