using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One tick row in the "New group" form: a community that sits under the chosen super community.
// Tapping the row toggles the tick.
public partial class AbSwitchPickItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    // "No. 12 - 5 groups"
    public string DetailText { get; private set; } = string.Empty;

    // Read by screen readers for the whole row.
    public string RowLabel
    {
        get { return IsOn ? Name + ", selected" : Name + ", not selected"; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowLabel))]
    private bool _isOn;

    [RelayCommand]
    private void Toggle()
    {
        IsOn = !IsOn;
    }

    public static AbSwitchPickItem FromCommunity(Community community)
    {
        AbSwitchPickItem item = new AbSwitchPickItem();
        item.Id = community.Id;
        item.Name = community.Name;
        item.DetailText = CommunityText.NumberText(community.Number) + " - " + CommunityText.Count(community.GroupIds.Count, "DTC", "DTCs");
        return item;
    }
}
