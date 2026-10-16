namespace GroupAnnouncementApp.Controls;

public partial class BlinkingDot : ContentView
{
    private const string BlinkName = "StatusDotBlink";

    public BlinkingDot()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsVisibleProperty.PropertyName)
        {
            UpdateBlink();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        UpdateBlink();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        StopBlink();
    }

    private void UpdateBlink()
    {
        if (IsVisible && Handler != null)
        {
            StartBlink();
        }
        else
        {
            StopBlink();
        }
    }

    // Bright, then almost gone, then bright again, about once a second.
    private void StartBlink()
    {
        this.AbortAnimation(BlinkName);

        Animation blink = new Animation();
        blink.Add(0.0, 0.5, new Animation(v => Opacity = v, 1.0, 0.15, Easing.Linear));
        blink.Add(0.5, 1.0, new Animation(v => Opacity = v, 0.15, 1.0, Easing.Linear));
        blink.Commit(this, BlinkName, 16, 1000, Easing.Linear, null, () => true);
    }

    private void StopBlink()
    {
        this.AbortAnimation(BlinkName);
        Opacity = 1.0;
    }
}
