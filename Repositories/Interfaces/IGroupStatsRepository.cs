using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;

namespace GroupAnnouncementApp.Repositories.Interfaces;

// Phase 3. groupStats/{groupId}. All methods throw on failure; the service layer decides what a failure means.
public interface IGroupStatsRepository
{
    Task<GroupStatsDocument?> GetAsync(string groupId);

    // Reads every stats document (one read each). Admin only.
    Task<List<GroupStatsDocument>> GetAllAsync();

    // Writes the whole document (admin).
    Task SetAsync(string groupId, GroupStatsDocument stats);

    // Phase 4: adds the write to a batch instead of sending it.
    void QueueSet(FirestoreBatch batch, string groupId, GroupStatsDocument stats);

    // Changes single fields (admin). Throws when the document does not exist.
    Task UpdateAsync(string groupId, params (string, object?)[] fields);

    // Member: adds 1 to panicCount or noticePanicCount. Returns false when the group has no stats document
    // (the press itself is still saved). Retries when another member pressed at the same moment.
    Task<bool> IncrementPanicAsync(string groupId, bool onNotice);

    // The one place that listens to the whole collection (admin). If the plugin's listener signature differs
    // from this, only this method needs changing.
    IDisposable WatchAll(Action<List<GroupStatsDocument>> onChanged, Action<Exception> onError);
}
