using CommunityToolkit.Mvvm.ComponentModel;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One row of the "Add members" list. The same instance is reused when the list is filtered,
// so a tick survives changing the search text.
public partial class SelectableUserItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

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

        return item;
    }
}
