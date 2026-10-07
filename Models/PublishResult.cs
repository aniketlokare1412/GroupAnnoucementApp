namespace GroupAnnouncementApp.Models;

// Outcome of posting one announcement to one or more groups.
public sealed class PublishResult
{
    public List<string> PostedGroupIds { get; set; } = new List<string>();

    public List<string> FailedGroupIds { get; set; } = new List<string>();

    // Why the failed groups failed (only set when FailedGroupIds is not empty).
    public string? ErrorMessage { get; set; }
}
