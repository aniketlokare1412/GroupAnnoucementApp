using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GroupAnnouncementApp.ViewModels;

// One tappable pill in the community edit page: a group (inside a community) or a community
// (inside a super community). Tapping toggles the tick.
public partial class SelectableOptionItem : ObservableObject
{
    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;

    // Short extra text such as "inactive" or "in North Zone". Empty when there is none.
    public string Hint { get; private set; } = string.Empty;

    // "Name (1234) - hint"
    public string PillText
    {
        get
        {
            string text = string.IsNullOrWhiteSpace(Number) ? Title : Title + " (" + Number + ")";
            if (!string.IsNullOrWhiteSpace(Hint))
            {
                text = text + " - " + Hint;
            }

            return text;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSelected))]
    [NotifyPropertyChangedFor(nameof(CardLabel))]
    private bool _isSelected;

    public bool IsNotSelected
    {
        get { return !IsSelected; }
    }

    // Read by screen readers for the whole pill.
    public string CardLabel
    {
        get { return IsSelected ? PillText + ", selected" : PillText + ", not selected"; }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsSelected = !IsSelected;
    }

    public static SelectableOptionItem Create(string id, string title, string number, string hint, bool isSelected)
    {
        SelectableOptionItem item = new SelectableOptionItem();
        item.Id = id;
        item.Title = title;
        item.Number = number;
        item.Hint = hint;
        item.IsSelected = isSelected;
        return item;
    }
}
