namespace GroupAnnouncementApp.Services.Interfaces;

// A short, non-blocking message such as "Group saved". View models call Show; a ToastView on the
// visible page displays it. Use it for quick feedback; keep error banners for errors the user must act on.
public interface IToastService
{
    event Action<string>? ToastRequested;

    void Show(string message);
}
