using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IGroupFeedRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    // null when the group has no feed document yet.
    Task<GroupFeedDocument?> GetAsync(string groupId);

    // Writes the whole document (replaces it).
    Task SetAsync(string groupId, GroupFeedDocument feed);

    // Phase 4: adds the write to a batch instead of sending it.
    void QueueSet(FirestoreBatch batch, string groupId, GroupFeedDocument feed);

    // Changes single fields. Throws when the document does not exist.
    Task UpdateAsync(string groupId, params (string, object?)[] fields);

    // Live listener on one group's feed. onChanged gets null when the document does not exist.
    // Dispose the returned object to stop listening.
    IDisposable Watch(string groupId, Action<GroupFeedDocument?> onChanged, Action<Exception> onError);
}
