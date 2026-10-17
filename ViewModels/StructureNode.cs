using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.ViewModels;

public enum StructureKind
{
    Super,
    Community,
    Group,
    Section
}

// One row of the Structure tree. The tree is built once per load; the page shows the rows that are
// currently visible (a flat list, indented by Depth), so expand and collapse are cheap.
public partial class StructureNode : ObservableObject
{
    public StructureKind Kind { get; private set; }

    // The Firestore id (empty for the two section headers).
    public string Id { get; private set; } = string.Empty;

    // Unique per place in the tree: a group in two communities appears twice with different keys,
    // so each copy keeps its own expanded / collapsed state.
    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string Caption { get; private set; } = string.Empty;
    public string Initials { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public bool IsInactive { get; private set; }
    public int Depth { get; private set; }
    public List<StructureNode> Children { get; } = new List<StructureNode>();

    public ICommand? OpenCommand { get; set; }
    public ICommand? ToggleCommand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronRotation))]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool _isExpanded;

    public bool HasChildren
    {
        get { return Children.Count > 0; }
    }

    public bool HasNoChildren
    {
        get { return Children.Count == 0; }
    }

    public bool IsSection
    {
        get { return Kind == StructureKind.Section; }
    }

    public bool IsNotSection
    {
        get { return Kind != StructureKind.Section; }
    }

    // The arrow points right when collapsed and down when open.
    public double ChevronRotation
    {
        get { return IsExpanded ? 90 : 0; }
    }

    public Thickness RowMargin
    {
        get { return new Thickness(Depth * 18, 0, 0, 0); }
    }

    public string ToggleLabel
    {
        get { return (IsExpanded ? "Collapse " : "Expand ") + Name; }
    }

    public string RowLabel
    {
        get { return Name + ", " + Caption; }
    }

    public static StructureNode Create(StructureKind kind, string id, string key, string name, string number, string caption, int depth, bool isInactive)
    {
        StructureNode node = new StructureNode();
        node.Kind = kind;
        node.Id = id;
        node.Key = key;
        node.Name = name;
        node.Number = number;
        node.Caption = caption;
        node.Depth = depth;
        node.IsInactive = isInactive;
        node.Initials = InitialsHelper.From(name);
        return node;
    }
}
