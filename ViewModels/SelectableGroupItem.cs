using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One card of the "Post to" picker in the announcement composer. The same instance is reused
// when the list is filtered, so a tick survives changing the search text.
// Tapping the card toggles the tick (same pattern as SelectableUserItem).
public partial class SelectableGroupItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelected))]
    [NotifyPropertyChangedFor(nameof(CardLabel))]
    private bool _isSelected;

    public bool IsNotSelected
    {
        get { return !IsSelected; }
    }

    public bool HasDescription
    {
        get { return !string.IsNullOrWhiteSpace(Description); }
    }

    // Read by screen readers for the whole card.
    public string CardLabel
    {
        get { return IsSelected ? Name + ", selected" : Name + ", not selected"; }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
    }

    public static SelectableGroupItem FromGroup(AnnouncementGroup group)
    {
        SelectableGroupItem item = new SelectableGroupItem();
        item.Id = group.Id;
        item.Name = group.Name;
        item.Description = group.Description;
        item.Initials = InitialsHelper.From(group.Name);
        return item;
    }
}
