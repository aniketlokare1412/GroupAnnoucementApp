using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Accessibility;

namespace GroupAnnouncementApp.Controls;

// Shows messages sent through IToastService. A page calls Attach in OnAppearing and Detach in
// OnDisappearing, so only the visible page ever shows a toast.
public partial class ToastView : ContentView
{
    private const int VisibleMilliseconds = 2500;

    private IToastService? _service;

    // Bumped for every toast, so an older toast never hides a newer one.
    private int _token;

    public ToastView()
    {
        InitializeComponent();
    }

    public void Attach(IToastService service)
    {
        Detach();
        _service = service;
        _service.ToastRequested += OnToastRequested;
    }

    public void Detach()
    {
        if (_service != null)
        {
            _service.ToastRequested -= OnToastRequested;
            _service = null;
        }
    }

    private void OnToastRequested(string message)
    {
        // The service can be called from any thread.
        Dispatcher.Dispatch(async () => await ShowAsync(message));
    }

    private async Task ShowAsync(string message)
    {
        _token++;
        int token = _token;

        MessageLabel.Text = message;
        SemanticScreenReader.Announce(message);

        Opacity = 0;
        IsVisible = true;
        await this.FadeToAsync(1, 150);

        await Task.Delay(VisibleMilliseconds);
        if (token != _token)
        {
            return;
        }

        await this.FadeToAsync(0, 200);
        if (token == _token)
        {
            IsVisible = false;
        }
    }
}
