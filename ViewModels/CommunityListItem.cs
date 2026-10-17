using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.ViewModels;

// One card of the admin Communities or Super communities list. The card's commands live on
// the card itself, so the page needs no cross-binding (same pattern as GroupListItem).
public sealed class CommunityListItem
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    // "No. 12 - 3 groups - Super: North zone"
    public string Subtitle { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    // Small chips under the subtitle: a community lists its groups, a super community its communities
    // (the first few, then "+N more"). Lets the admin see what is inside without opening the editor.
    public List<string> Chips { get; private set; } = new List<string>();

    public bool HasChips
    {
        get { return Chips.Count > 0; }
    }

    public string Initials { get; private set; } = string.Empty;

    // Tap on the card and the pencil button: edit this item.
    public ICommand? EditCommand { get; private set; }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    public string EditLabel
    {
        get { return "Edit " + Name; }
    }

    public static CommunityListItem Create(string id, string name, string subtitle, string description, Func<CommunityListItem, Task> onEdit, List<string>? chips = null)
    {
        CommunityListItem item = new CommunityListItem();
        item.Id = id;
        item.Name = name;
        item.Subtitle = subtitle;
        item.Description = description;
        item.Chips = chips ?? new List<string>();
        item.Initials = InitialsHelper.From(name);
        item.EditCommand = new AsyncRelayCommand(() => onEdit(item));
        return item;
    }
}
