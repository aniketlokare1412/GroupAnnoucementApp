using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class ToastService : IToastService
{
    public event Action<string>? ToastRequested;

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ToastRequested?.Invoke(message);
    }
}
