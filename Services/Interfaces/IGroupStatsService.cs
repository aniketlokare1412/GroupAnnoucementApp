using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;

namespace GroupAnnouncementApp.Services.Interfaces;

// Phase 3: keeps groupStats/{groupId} (what the admin Home and Groups tab read) in step with posts, completions,
// deletions and panic presses.
// The per-group methods NEVER throw: the stats are a convenience for the admin screens, so a failure is logged
// and must not fail a post or a panic report. "Rebuild stats" (Account tab) repairs anything that was missed.
public interface IGroupStatsService
{
    // A new announcement was posted: no reports on it, the 5-minute notice reports are cleared, "last posted" is now.
    Task PostedAsync(string groupId);

    // Phase 4: adds the "fresh post" stats document to the batch.
    void QueuePosted(FirestoreBatch batch, string groupId);

    // The open announcement was marked completed (the reports that count are now the notice's).
    Task CompletedAsync(string groupId);

    // The open announcement was deleted (members see the 5-minute notice again).
    Task RemovedAsync(string groupId);

    // A member pressed the panic button on this announcement (or on the notice, id "notice"): adds 1 to the counter.
    Task PanicPressedAsync(string groupId, string announcementId);

    // A group without a stats document: works it out the old way (announcements + press documents), saves it
    // for next time and returns it. Null when that failed.
    Task<GroupStatsDocument?> RepairGroupAsync(string groupId);

    // Admin migration (Account tab): rebuilds the stats document of EVERY group from the announcements and the
    // press documents. Safe to repeat. Costs a few reads per group (the press documents are counted once, here)
    // and one write per group.
    Task<OperationResult<int>> RebuildAllAsync();
}
