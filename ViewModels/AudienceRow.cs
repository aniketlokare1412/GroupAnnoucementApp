using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.ViewModels;

// One row of the audience picker: a group, a community or a super community.
// A community or super community is ticked when ALL of its active groups are ticked,
// "some" when only part of them are.
public partial class AudienceRow : ObservableObject
{
    private int _total;
    private string _baseDetail = string.Empty;

    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public bool IsGroup { get; private set; }

    // The active groups this row stands for (one id for a group).
    public List<string> GroupIds { get; private set; } = new List<string>();

    public ICommand? ToggleCommand { get; private set; }

    // 0 = none ticked, 1 = some, 2 = all.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNone))]
    [NotifyPropertyChangedFor(nameof(IsSome))]
    [NotifyPropertyChangedFor(nameof(IsAll))]
    [NotifyPropertyChangedFor(nameof(CardLabel))]
    private int _state;

    [ObservableProperty]
    private string _detailText = string.Empty;

    public bool IsNone
    {
        get { return State == 0; }
    }

    public bool IsSome
    {
        get { return State == 1; }
    }

    public bool IsAll
    {
        get { return State == 2; }
    }

    // Read by screen readers.
    public string CardLabel
    {
        get
        {
            string state = State == 2 ? "all selected" : State == 1 ? "some selected" : "not selected";
            return Name + ", " + state;
        }
    }

    public static AudienceRow Create(string id, string name, string number, bool isGroup, string baseDetail, List<string> groupIds, Action<AudienceRow> onToggle)
    {
        AudienceRow row = new AudienceRow();
        row.Id = id;
        row.Name = name;
        row.Number = number;
        row.IsGroup = isGroup;
        row._baseDetail = baseDetail;
        row._total = groupIds.Count;
        row.GroupIds = groupIds;
        row.DetailText = baseDetail;
        row.ToggleCommand = new RelayCommand(() => onToggle(row));
        return row;
    }

    // Recomputes the tick state from the set of ticked group ids.
    public void Refresh(HashSet<string> selected)
    {
        int count = 0;
        for (int i = 0; i < GroupIds.Count; i++)
        {
            if (selected.Contains(GroupIds[i]))
            {
                count++;
            }
        }

        State = count == 0 ? 0 : (count == _total ? 2 : 1);

        if (IsGroup)
        {
            DetailText = _baseDetail;
        }
        else
        {
            DetailText = count == 0 ? _baseDetail : count + " of " + CommunityText.Count(_total, "group", "groups") + " selected";
        }
    }
}
