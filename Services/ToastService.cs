using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class ToastService : IToastService
{
    // A pending message is only worth showing if the next page appears right away.
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromSeconds(5);

    private string? _pending;
    private DateTime _pendingAtUtc;

    public event Action<string>? ToastRequested;

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Action<string>? handler = ToastRequested;
        if (handler == null)
        {
            // Nobody is listening (the page is switching). Keep it for the next page.
            _pending = message;
            _pendingAtUtc = DateTime.UtcNow;
            return;
        }

        handler(message);
    }

    public string? TakePending()
    {
        string? message = _pending;
        _pending = null;

        if (message == null || DateTime.UtcNow - _pendingAtUtc > PendingLifetime)
        {
            return null;
        }

        return message;
    }
}
