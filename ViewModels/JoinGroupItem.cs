using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one card of the Discover list shows. Rebuilt whenever the list is reloaded.
// Not joined: the card and the plus button ask to join. Joined: the card opens the group's announcements.
public sealed class JoinGroupItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public bool IsMember { get; private set; }

    // Tap on the card: join (not a member) or open announcements (member).
    public ICommand? PrimaryCommand { get; private set; }

    // The plus button (only shown when not a member).
    public ICommand? JoinCommand { get; private set; }

    public bool IsNotMember
    {
        get { return !IsMember; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    // Read by screen readers for the icon-only plus button.
    public string JoinLabel
    {
        get { return "Join " + Name; }
    }

    // Read by screen readers for the whole card.
    public string CardLabel
    {
        get { return IsMember ? Name + ", joined" : Name + ", not joined"; }
    }

    public string CardHint
    {
        get { return IsMember ? "Opens announcements" : "Asks to join this group"; }
    }

    public static JoinGroupItem FromJoinable(
        JoinableGroup joinable,
        Func<JoinGroupItem, Task> onJoin,
        Func<JoinGroupItem, Task> onOpen)
    {
        JoinGroupItem item = new JoinGroupItem();
        item.Id = joinable.Group.Id;
        item.Name = joinable.Group.Name;
        item.Description = joinable.Group.Description;
        item.Initials = InitialsHelper.From(joinable.Group.Name);
        item.IsMember = joinable.IsMember;
        item.JoinCommand = new AsyncRelayCommand(() => onJoin(item));

        if (item.IsMember)
        {
            item.PrimaryCommand = new AsyncRelayCommand(() => onOpen(item));
        }
        else
        {
            item.PrimaryCommand = item.JoinCommand;
        }

        return item;
    }
}
