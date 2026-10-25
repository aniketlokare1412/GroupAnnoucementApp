using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GroupAnnouncementApp.ViewModels;

// One community in the dropdown of the Communities tab (a tick box).
public partial class CommunityOptionRow : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    // "No. 12 - 5 groups"
    public string Detail { get; private set; } = string.Empty;

    public ICommand? ToggleCommand { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff))]
    [NotifyPropertyChangedFor(nameof(CardLabel))]
    private bool _isOn;

    public bool IsOff
    {
        get { return !IsOn; }
    }

    public string CardLabel
    {
        get { return Name + ", " + Detail + (IsOn ? ", selected" : ", not selected"); }
    }

    public static CommunityOptionRow Create(string id, string name, string detail, bool isOn, Action<CommunityOptionRow> onToggle)
    {
        CommunityOptionRow row = new CommunityOptionRow();
        row.Id = id;
        row.Name = name;
        row.Detail = detail;
        row.IsOn = isOn;
        row.ToggleCommand = new RelayCommand(() => onToggle(row));
        return row;
    }
}

// One group in the read-only list of the Communities tab.
public sealed class HierarchyGroupRow
{
    public string Name { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;

    public static HierarchyGroupRow Create(string name, string detail)
    {
        HierarchyGroupRow row = new HierarchyGroupRow();
        row.Name = name;
        row.Detail = detail;
        return row;
    }
}
