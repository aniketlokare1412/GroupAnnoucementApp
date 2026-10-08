namespace GroupAnnouncementApp.Services.Interfaces;

// A short, non-blocking message such as "Group saved". View models call Show; a ToastView on the
// visible page displays it. Use it for quick feedback; keep error banners for errors the user must act on.
public interface IToastService
{
    event Action<string>? ToastRequested;

    void Show(string message);

    // Phase B: a message shown while no page was listening (for example right after going back
    // to the previous page). A ToastView asks for it when its page appears. Messages older than a
    // few seconds are dropped. Returns null when there is nothing to show.
    string? TakePending();
}
