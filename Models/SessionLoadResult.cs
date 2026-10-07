namespace GroupAnnouncementApp.Models;

public sealed class SessionLoadResult
{
    public SessionStatus Status { get; private set; }

    // A user-friendly message for the Deactivated and LoadFailed cases.
    public string? Message { get; private set; }

    // Where the app should go next. Null means "stay where you are" (LoadFailed with retry).
    public string? Route { get; private set; }

    public static SessionLoadResult Create(SessionStatus status, string? message, string? route)
    {
        SessionLoadResult result = new SessionLoadResult();
        result.Status = status;
        result.Message = message;
        result.Route = route;
        return result;
    }
}
