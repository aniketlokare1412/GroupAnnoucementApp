namespace GroupAnnouncementApp.Controls;

// Loading placeholder for lists. Show it with IsVisible bound to the page's ShowLoading flag.
// Kind = "Row" (cards with a tile and two lines), "Post" (announcement cards) or "Pills" (group pills).
public partial class SkeletonList : ContentView
{
    private const string PulseName = "SkeletonPulse";

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind),
        typeof(string),
        typeof(SkeletonList),
        "Row",
        propertyChanged: OnKindChanged);

    public string Kind
    {
        get { return (string)GetValue(KindProperty); }
        set { SetValue(KindProperty, value); }
    }

    public SkeletonList()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnKindChanged(BindableObject bindable, object oldValue, object newValue)
    {
        SkeletonList list = (SkeletonList)bindable;
        string kind = (newValue as string) ?? "Row";
        bool isPost = string.Equals(kind, "Post", StringComparison.OrdinalIgnoreCase);
        bool isPills = string.Equals(kind, "Pills", StringComparison.OrdinalIgnoreCase);
        list.RowKind.IsVisible = !isPost && !isPills;
        list.PostKind.IsVisible = isPost;
        list.PillsKind.IsVisible = isPills;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsVisibleProperty.PropertyName)
        {
            UpdatePulse();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdatePulse();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        StopPulse();
    }

    private void UpdatePulse()
    {
        if (IsVisible && Handler != null)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    // Fades down and back up, forever, until the control is hidden.
    private void StartPulse()
    {
        this.AbortAnimation(PulseName);

        Animation pulse = new Animation();
        pulse.Add(0.0, 0.5, new Animation(v => Opacity = v, 1.0, 0.45, Easing.SinInOut));
        pulse.Add(0.5, 1.0, new Animation(v => Opacity = v, 0.45, 1.0, Easing.SinInOut));
        pulse.Commit(this, PulseName, 16, 1500, Easing.Linear, null, () => true);
    }

    private void StopPulse()
    {
        this.AbortAnimation(PulseName);
        Opacity = 1.0;
    }
}
