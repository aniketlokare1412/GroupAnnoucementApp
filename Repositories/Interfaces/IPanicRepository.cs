namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IPanicRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.
    Task CreateAsync(string groupId, string announcementId, string uid);

    Task<bool> ExistsAsync(string groupId, string announcementId, string uid);

    // When this user pressed last on this announcement, or null if never.
    Task<DateTimeOffset?> LastPressAsync(string groupId, string announcementId, string uid);

    // Admin only (enforced by the Firestore rules).
    Task<int> CountAsync(string groupId, string announcementId);

    Task DeleteAllAsync(string groupId, string announcementId);
}
