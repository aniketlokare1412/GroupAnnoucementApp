using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One card of the "Add members" list. The same instance is reused when the list is filtered,
// so a tick survives changing the search text. Tapping the card toggles the tick.
public partial class SelectableUserItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelected))]
    [NotifyPropertyChangedFor(nameof(CardLabel))]
    private bool _isSelected;

    public bool IsNotSelected
    {
        get { return !IsSelected; }
    }

    public bool HasEmail
    {
        get { return !string.IsNullOrWhiteSpace(Email); }
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

    public static SelectableUserItem FromProfile(UserProfile profile)
    {
        SelectableUserItem item = new SelectableUserItem();
        item.Id = profile.Id;
        item.Email = profile.Email;
        item.Phone = profile.Phone;

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            item.Name = profile.Email;
        }
        else
        {
            item.Name = profile.Name;
        }

        item.Initials = InitialsHelper.From(item.Name);
        return item;
    }
}
