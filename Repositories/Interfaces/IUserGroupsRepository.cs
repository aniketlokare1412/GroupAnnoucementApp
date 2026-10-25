using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IUserGroupsRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    // null when the user has no userGroups document yet.
    Task<List<string>?> GetAsync(string userId);

    // Writes the whole list (replaces it).
    Task SetAsync(string userId, List<string> groupIds);

    // Live listener on the user's own document. onChanged gets null when the document does not exist.
    IDisposable Watch(string userId, Action<List<string>?> onChanged, Action<Exception> onError);
}
