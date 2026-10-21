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
    private DateTimeOffset? _lastPressAt;
    private bool _clockRegistered;
    private int _panicCount;

    // Presses made in this session, so a card that is rebuilt (Home refreshes) keeps its 5-minute wait at once.
    private static readonly Dictionary<string, DateTimeOffset> RecentPresses = new Dictionary<string, DateTimeOffset>();

    private string CacheKey
    {
        get { return _groupId + "/" + _announcementId; }
    }

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

        lock (RecentPresses)
        {
            DateTimeOffset cached;
            if (RecentPresses.TryGetValue(CacheKey, out cached))
            {
                _lastPressAt = cached;
            }
        }

        // While the countdown runs, one shared clock ticks every second (see Register).
        EnsureClock();

        PressPanicCommand = new AsyncRelayCommand(PressPanicAsync);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync);
    }

    // The panic button under the 5-minute notice: the same button, enabled from the start. There is no
    // announcement, so the presses go to the fixed "notice" id of the group.
    public static AnnouncementExtras ForNotice(string groupId, IPanicService panics, IDialogService dialogs, IToastService toasts)
    {
        Announcement stand = new Announcement();
        stand.Id = PanicLimits.NoticeAnnouncementId;
        stand.GroupId = groupId;
        stand.Status = AnnouncementStatus.Active;
        stand.ResolveMinutes = 0;
        return new AnnouncementExtras(groupId, stand, false, null!, panics, dialogs, toasts);
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
                return Color.FromArgb("#65A30D");
            }

            if (ShowTimeUp)
            {
                return Color.FromArgb("#F59E0B");
            }

            return Color.FromArgb("#D1402F");
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

    // The panic button opens when the resolution time has passed, or at once when there is no resolution time.
    private bool IsUnlocked
    {
        get
        {
            if (_isCompleted)
            {
                return true;
            }

            return !HasResolution || ShowTimeUp;
        }
    }

    // After a press the button waits 5 minutes before it can be pressed again.
    private DateTimeOffset CooldownEndsAt
    {
        get { return _lastPressAt == null ? DateTimeOffset.MinValue : _lastPressAt.Value.AddMinutes(PanicLimits.CooldownMinutes); }
    }

    private bool InCooldown
    {
        get { return _lastPressAt != null && DateTimeOffset.UtcNow < CooldownEndsAt; }
    }

    // The shared clock runs while the countdown or the panic wait is on screen.
    private bool NeedsClock
    {
        get { return ShowTimer || InCooldown; }
    }

    // Members only.
    public bool ShowPanic
    {
        get { return !_isAdmin; }
    }

    // Members always see the button. It is greyed out while the time has not run out and for 5 minutes after a press.
    public bool ShowPanicButton
    {
        get { return !_isAdmin; }
    }

    public bool CanPressPanic
    {
        get { return !_isAdmin && IsUnlocked && !InCooldown; }
    }

    public string PanicText
    {
        get
        {
            if (!IsUnlocked)
            {
                return "Available when the time is up";
            }

            if (InCooldown)
            {
                TimeSpan left = CooldownEndsAt - DateTimeOffset.UtcNow;
                if (left < TimeSpan.Zero)
                {
                    left = TimeSpan.Zero;
                }

                return "Reported. Report again in " + left.Minutes.ToString("00") + ":" + left.Seconds.ToString("00");
            }

            return _lastPressAt != null ? "Still not solved? Report again" : "Problem not solved? Report it";
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

    // Loads what the card needs from Firestore: a member's last report, or an admin's count.
    public async Task LoadAsync()
    {
        if (_isAdmin)
        {
            OperationResult<int> count = await _panics.CountAsync(_groupId, _announcementId);
            if (count.IsSuccess)
            {
                _panicCount = count.Value;
            }
        }
        else
        {
            OperationResult<DateTimeOffset?> last = await _panics.GetLastPressAsync(_groupId, _announcementId);
            if (last.IsSuccess)
            {
                _lastPressAt = last.Value;
                lock (RecentPresses)
                {
                    if (last.Value != null)
                    {
                        RecentPresses[CacheKey] = last.Value.Value;
                    }
                    else
                    {
                        RecentPresses.Remove(CacheKey);
                    }
                }
            }

            EnsureClock();
        }

        // An empty name means "everything changed".
        OnPropertyChanged(string.Empty);
    }

    // One shared clock for every card with a running countdown. Cards are held weakly, so cards that
    // were replaced by a refresh simply drop out.
    private static readonly List<WeakReference<AnnouncementExtras>> Live = new List<WeakReference<AnnouncementExtras>>();
    private static IDispatcherTimer? _ticker;

    // Joins the shared clock once, and only while it is needed.
    private void EnsureClock()
    {
        if (!_clockRegistered && NeedsClock)
        {
            _clockRegistered = true;
            Register(this);
        }
    }

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
        if (NeedsClock)
        {
            OnPropertyChanged(nameof(TimeLeftText));
            OnPropertyChanged(nameof(PanicText));
            return true;
        }

        // The time ran out, the 5-minute wait ended or the work was completed: refresh everything.
        OnPropertyChanged(string.Empty);
        _clockRegistered = false;
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
            "Report that the problem is still not solved? You can report again after " + PanicLimits.CooldownMinutes + " minutes.",
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

        _lastPressAt = DateTimeOffset.UtcNow;
        lock (RecentPresses)
        {
            RecentPresses[CacheKey] = _lastPressAt.Value;
        }

        _panicCount++;
        EnsureClock();
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
