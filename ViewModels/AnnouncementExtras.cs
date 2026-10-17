using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Everything on an announcement card about its status: Active / Completed, the expected resolution time,
// the panic button (members) and "Mark completed" with the panic count (admins).
public sealed class AnnouncementExtras : ObservableObject
{
    private readonly string _groupId;
    private readonly string _announcementId;
    private readonly bool _isAdmin;
    private readonly DateTimeOffset _createdAt;
    private readonly int _resolveMinutes;
    private long _completedAtMs;
    private readonly IAnnouncementService _announcements;
    private readonly IPanicService _panics;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private bool _isCompleted;
    private bool _hasPressed;
    private int _panicCount;

    public AnnouncementExtras(
        string groupId,
        Announcement announcement,
        bool isAdmin,
        IAnnouncementService announcements,
        IPanicService panics,
        IDialogService dialogs,
        IToastService toasts)
    {
        _groupId = groupId;
        _announcementId = announcement.Id;
        _isAdmin = isAdmin;
        _createdAt = announcement.CreatedAt;
        _resolveMinutes = announcement.ResolveMinutes;
        _isCompleted = announcement.Status == AnnouncementStatus.Completed;
        _completedAtMs = announcement.CompletedAtMs;
        _announcements = announcements;
        _panics = panics;
        _dialogs = dialogs;
        _toasts = toasts;

        // While the countdown runs, one shared clock ticks every second (see Register).
        if (ShowTimer)
        {
            Register(this);
        }

        PressPanicCommand = new AsyncRelayCommand(PressPanicAsync);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync);
    }

    public ICommand PressPanicCommand { get; private set; }
    public ICommand CompleteCommand { get; private set; }

    public bool IsCompleted
    {
        get { return _isCompleted; }
    }

    public bool IsActive
    {
        get { return !_isCompleted; }
    }

    // The stripe on the left of the card: green = completed, amber = time is up, red = work in progress.
    public Color AccentColor
    {
        get
        {
            if (_isCompleted)
            {
                return Color.FromArgb("#16A34A");
            }

            if (ShowTimeUp)
            {
                return Color.FromArgb("#F59E0B");
            }

            return Color.FromArgb("#DC2626");
        }
    }

    // "Completed at 11:20 AM, 13 Oct" (the time the admin tapped Mark completed).
    public string CompletedText
    {
        get
        {
            if (_completedAtMs <= 0)
            {
                return "Completed";
            }

            return "Completed at " + DateTimeOffset.FromUnixTimeMilliseconds(_completedAtMs).ToLocalTime().ToString("hh:mm tt, dd MMM");
        }
    }

    private DateTimeOffset ResolveAt
    {
        get { return _createdAt.AddMinutes(_resolveMinutes); }
    }

    // The countdown is on screen while there is a resolution time, it has not passed and the work is not completed.
    public bool ShowTimer
    {
        get { return HasResolution && !_isCompleted && DateTimeOffset.UtcNow < ResolveAt; }
    }

    // The time has passed and the work is still not completed.
    public bool ShowTimeUp
    {
        get { return HasResolution && !_isCompleted && DateTimeOffset.UtcNow >= ResolveAt; }
    }

    // "00:12:34" (hours:minutes:seconds).
    public string TimeLeftText
    {
        get
        {
            TimeSpan left = ResolveAt - DateTimeOffset.UtcNow;
            if (left < TimeSpan.Zero)
            {
                left = TimeSpan.Zero;
            }

            return ((int)left.TotalHours).ToString("00") + ":" + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
        }
    }

    // More than 5 presses: this announcement shows the extra "single phase" message.
    public bool ShowSubMessage
    {
        get { return _panicCount > MessageTemplates.PanicSubMessageThreshold; }
    }

    public string SubMessageTitle
    {
        get { return MessageTemplates.SinglePhaseTitle; }
    }

    public string SubMessageBody
    {
        get { return MessageTemplates.SinglePhaseBody; }
    }

    public bool HasPanic
    {
        get { return _panicCount > 0; }
    }

    public bool HasNoPanic
    {
        get { return _panicCount == 0; }
    }

    public bool HasResolution
    {
        get { return _resolveMinutes > 0 && _createdAt != default; }
    }

    public string ResolutionText
    {
        get
        {
            if (!HasResolution)
            {
                return string.Empty;
            }

            return "Expected resolution: " + _createdAt.AddMinutes(_resolveMinutes).ToLocalTime().ToString("hh:mm tt, dd MMM");
        }
    }

    // The panic button opens when the work is completed or the resolution time has passed.
    private bool IsUnlocked
    {
        get
        {
            if (_isCompleted)
            {
                return true;
            }

            return ShowTimeUp;
        }
    }

    // Members only.
    public bool ShowPanic
    {
        get { return !_isAdmin; }
    }

    // The panic button is only drawn once it is open (or already used, then it says "Reported").
    // While it is locked, a small caption explains when it opens instead.
    public bool ShowPanicButton
    {
        get { return !_isAdmin && (IsUnlocked || _hasPressed); }
    }

    public bool ShowPanicHint
    {
        get { return !_isAdmin && !IsUnlocked && !_hasPressed; }
    }

    public string PanicHintText
    {
        get { return "Problem report opens when the time is up or the work is completed."; }
    }

    public bool CanPressPanic
    {
        get { return !_isAdmin && !_hasPressed && IsUnlocked; }
    }

    public string PanicText
    {
        get
        {
            if (_hasPressed)
            {
                return "Reported";
            }

            if (IsUnlocked)
            {
                return "Problem not solved? Report it";
            }

            return "Available when the time is up or the work is completed";
        }
    }

    // Admins only.
    public bool ShowAdminTools
    {
        get { return _isAdmin; }
    }

    public bool CanMarkCompleted
    {
        get { return _isAdmin && !_isCompleted; }
    }

    public string PanicCountText
    {
        get { return "Panic reports: " + _panicCount; }
    }

    // Loads what the card needs from Firestore: a member's own report, or an admin's count.
    public async Task LoadAsync()
    {
        // Everyone counts the presses (members may read the anonymous press records).
        OperationResult<int> count = await _panics.CountAsync(_groupId, _announcementId);
        if (count.IsSuccess)
        {
            _panicCount = count.Value;
        }

        if (!_isAdmin)
        {
            OperationResult<bool> pressed = await _panics.HasPressedAsync(_groupId, _announcementId);
            if (pressed.IsSuccess)
            {
                _hasPressed = pressed.Value;
            }
        }

        // An empty name means "everything changed".
        OnPropertyChanged(string.Empty);
    }

    // One shared clock for every card with a running countdown. Cards are held weakly, so cards that
    // were replaced by a refresh simply drop out.
    private static readonly List<WeakReference<AnnouncementExtras>> Live = new List<WeakReference<AnnouncementExtras>>();
    private static IDispatcherTimer? _ticker;

    private static void Register(AnnouncementExtras extras)
    {
        Live.Add(new WeakReference<AnnouncementExtras>(extras));

        if (_ticker == null)
        {
            IDispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _ticker = dispatcher.CreateTimer();
            _ticker.Interval = TimeSpan.FromSeconds(1);
            _ticker.Tick += OnTick;
        }

        if (!_ticker.IsRunning)
        {
            _ticker.Start();
        }
    }

    private static void OnTick(object? sender, EventArgs e)
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            AnnouncementExtras? extras;
            if (Live[i].TryGetTarget(out extras) && extras.Tick())
            {
                continue;
            }

            Live.RemoveAt(i);
        }

        if (Live.Count == 0 && _ticker != null)
        {
            _ticker.Stop();
        }
    }

    // Returns true while this card still needs the clock.
    private bool Tick()
    {
        if (ShowTimer)
        {
            OnPropertyChanged(nameof(TimeLeftText));
            return true;
        }

        // The time just ran out (or the work was completed): refresh everything, the panic button opens.
        OnPropertyChanged(string.Empty);
        return false;
    }

    private async Task PressPanicAsync()
    {
        if (!CanPressPanic)
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Report a problem",
            "Report that the problem is still not solved? You can report only once.",
            "Report",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        OperationResult result = await _panics.PressAsync(_groupId, _announcementId);
        if (!result.IsSuccess)
        {
            _toasts.Show(result.ErrorMessage ?? "We could not send your report.");
            return;
        }

        _hasPressed = true;
        _panicCount++;
        AlertFeedback.Buzz();
        OnPropertyChanged(string.Empty);
        _toasts.Show("Your report was sent");
    }

    private async Task CompleteAsync()
    {
        if (!CanMarkCompleted)
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Mark as completed",
            "Mark this work as completed? Members will then be able to report if the problem is not solved.",
            "Mark completed",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        OperationResult result = await _announcements.MarkCompletedAsync(_groupId, _announcementId);
        if (!result.IsSuccess)
        {
            _toasts.Show(result.ErrorMessage ?? "We could not mark it as completed.");
            return;
        }

        _isCompleted = true;
        _completedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        OnPropertyChanged(string.Empty);
        _toasts.Show("Marked as completed");
    }
}
