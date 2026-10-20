using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// The panic button: a member can press it when the resolution time has passed (or there is none),
// and again 5 minutes after each press.
public interface IPanicService
{
    Task<OperationResult> PressAsync(string groupId, string announcementId);

    Task<OperationResult<bool>> HasPressedAsync(string groupId, string announcementId);

    // When the signed-in member pressed last on this announcement (null = never).
    Task<OperationResult<DateTimeOffset?>> GetLastPressAsync(string groupId, string announcementId);

    // Admin: how many people pressed.
    Task<OperationResult<int>> CountAsync(string groupId, string announcementId);

    // Admin: the count for the latest announcement of each group (groups without presses count 0).
    Task<Dictionary<string, int>> GetLatestCountsAsync(List<string> groupIds);

    // Admin Home: panic count, last post time and completed flag of each group's newest announcement.
    Task<Dictionary<string, GroupActivity>> GetLatestActivityAsync(List<string> groupIds);
}
