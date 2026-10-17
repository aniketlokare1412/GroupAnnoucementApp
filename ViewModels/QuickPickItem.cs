using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace GroupAnnouncementApp.ViewModels;

// One "select a whole community" chip in the announcement composer.
// Tapping it ticks every ACTIVE group inside (the same per-group copies are posted as always).
public sealed class QuickPickItem
{
    public string PillText { get; private set; } = string.Empty;

    // Read by screen readers.
    public string Label { get; private set; } = string.Empty;

    public List<string> GroupIds { get; private set; } = new List<string>();

    public ICommand? SelectCommand { get; private set; }

    public static QuickPickItem Create(string name, bool isSuper, List<string> activeGroupIds, Action<QuickPickItem> onSelect)
    {
        string kind = isSuper ? "Super: " : "Community: ";

        QuickPickItem item = new QuickPickItem();
        item.PillText = kind + name + " (" + activeGroupIds.Count + ")";
        item.Label = "Select all " + activeGroupIds.Count + " active groups in " + (isSuper ? "super community " : "community ") + name;
        item.GroupIds = activeGroupIds;
        item.SelectCommand = new RelayCommand(() => onSelect(item));
        return item;
    }
}
