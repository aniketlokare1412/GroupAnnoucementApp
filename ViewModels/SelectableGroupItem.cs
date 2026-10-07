using CommunityToolkit.Mvvm.ComponentModel;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One row of the "post to several groups" picker. The same instance is reused when the
// list is filtered, so a tick survives changing the search text.
public partial class SelectableGroupItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    public static SelectableGroupItem FromGroup(AnnouncementGroup group)
    {
        SelectableGroupItem item = new SelectableGroupItem();
        item.Id = group.Id;
        item.Name = group.Name;
        return item;
    }
}
