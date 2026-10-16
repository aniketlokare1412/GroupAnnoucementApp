using GroupAnnouncementApp.Helpers;

namespace GroupAnnouncementApp.Services.Interfaces;

// The panic button: a member can press it once per announcement, after the resolution time has passed
// or the admin marked the work completed (the Firestore rules check this with the server time).
public interface IPanicService
{
    Task<OperationResult> PressAsync(string groupId, string announcementId);

    Task<OperationResult<bool>> HasPressedAsync(string groupId, string announcementId);

    // Admin: how many people pressed.
    Task<OperationResult<int>> CountAsync(string groupId, string announcementId);

    // Admin: the count for the latest announcement of each group (groups without presses count 0).
    Task<Dictionary<string, int>> GetLatestCountsAsync(List<string> groupIds);
}
