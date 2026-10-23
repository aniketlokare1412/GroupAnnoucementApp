using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly IToastService _toasts;
    private readonly JoinGroupsViewModel _joinViewModel;

    // Members and admins whose live listeners could not start (for example the new rules are not deployed yet) check for new data every 20 seconds while Home is on screen.
    private const int RefreshSeconds = 20;

    // Members with live listeners get new messages at once, with no reads. This slower timer only re-checks the
    // cards against the clock (a planned outage coming into view or ending); it never reads from the server.
    private const int LocalTickSeconds = 30;
    private IDispatcherTimer? _timer;
    private Window? _window;

    // Coming back to Home (admin): wait for the slide-back animation to finish before any work starts,
    // so the screen does not stop for a moment while it is still moving. The first visit loads at once.
    private const int TransitionMs = 300;
    private bool _appearedBefore;
    private bool _isShowing;

    public HomePage(HomeViewModel viewModel, IToastService toasts, JoinGroupsViewModel joinViewModel)
    {
        _joinViewModel = joinViewModel;
        InitializeComponent();
        _viewModel = viewModel;
        _toasts = toasts;
        BindingContext = viewModel;
        JoinSection.BindingContext = joinViewModel;
        joinViewModel.Joined += OnJoined;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isShowing = true;
        Toast.Attach(_toasts);

        bool returning = _appearedBefore;
        _appearedBefore = true;
        if (returning && _viewModel.IsAdmin)
        {
            await Task.Delay(TransitionMs);
            if (!_isShowing)
            {
                return;
            }
        }

        await _viewModel.LoadAsync();
        if (!_isShowing)
        {
            return;
        }

        if (_viewModel.IsMember)
        {
            await _joinViewModel.LoadAsync();
        }

        AttachWindow();
        StartTimer();
    }

    // The 20-second refresh stops while the app is in the background (every refresh costs reads),
    // and one refresh runs at once when the app comes back, so nothing is missed.
    private void AttachWindow()
    {
        DetachWindow();
        _window = Window;
        if (_window != null)
        {
            _window.Stopped += OnWindowStopped;
            _window.Resumed += OnWindowResumed;
        }
    }

    private void DetachWindow()
    {
        if (_window != null)
        {
            _window.Stopped -= OnWindowStopped;
            _window.Resumed -= OnWindowResumed;
            _window = null;
        }
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        StopTimer();
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        await _viewModel.LoadAsync();
        StartTimer();
    }

    // After joining a group, show its announcements right away.
    // After joining a group, show its announcements right away.
    private async void OnJoined(object? sender, EventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        if (_viewModel.UsesPolling)
        {
            _timer.Interval = TimeSpan.FromSeconds(RefreshSeconds);
            _timer.Tick += OnTimerTick;
        }
        else
        {
            _timer.Interval = TimeSpan.FromSeconds(LocalTickSeconds);
            _timer.Tick += OnLocalTick;
        }

        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer.Tick -= OnLocalTick;
            _timer = null;
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        // LoadAsync does nothing while another load is running, and it never shows the spinner again.
        await _viewModel.LoadAsync();
    }

    private async void OnLocalTick(object? sender, EventArgs e)
    {
        await _viewModel.TickLocalAsync();
    }

    protected override void OnDisappearing()
    {
        _isShowing = false;
        StopTimer();
        DetachWindow();
        Toast.Detach();
        base.OnDisappearing();
    }
}
