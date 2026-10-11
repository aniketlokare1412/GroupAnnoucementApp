using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly IToastService _toasts;
    private readonly JoinGroupsViewModel _joinViewModel;

    // While Home is on screen it checks for new announcements every 20 seconds.
    private const int RefreshSeconds = 20;
    private IDispatcherTimer? _timer;

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
        Toast.Attach(_toasts);
        await _viewModel.LoadAsync();
        if (_viewModel.IsMember)
        {
            await _joinViewModel.LoadAsync();
        }

        StartTimer();
    }

    // After joining a group, show its announcements right away.
    private async void OnJoined(object? sender, EventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(RefreshSeconds);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        // LoadAsync does nothing while another load is running, and it never shows the spinner again.
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        Toast.Detach();
        base.OnDisappearing();
    }
}
