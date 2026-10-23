using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;

namespace GroupAnnouncementApp.Services.Interfaces;

// Phase 2, admin side: keeps groupFeed/{groupId} (what members listen to) in step with the announcements and outages.
// The per-group methods THROW on failure: the posting, outage and announcement services call them inside their
// own try / catch, right after the main write.
public interface IFeedService
{
    // A new announcement was created for the group (the outage already in the feed is kept).
    Task PostedAsync(string groupId, Announcement announcement);

    // Phase 4: reads the group's current feed (for its outage), then adds the new feed document to the batch.
    // Nothing is added when the read fails.
    Task QueuePostedAsync(FirestoreBatch batch, string groupId, Announcement announcement);

    Task AnnouncementEditedAsync(string groupId, string title, string message);

    Task AnnouncementCompletedAsync(string groupId, long completedAtMs);

    // The group's announcement was deleted: members see the 5-minute notice again.
    Task AnnouncementRemovedAsync(string groupId);

    Task OutageScheduledAsync(string groupId, Outage outage);

    Task OutageCancelledAsync(string groupId, long cancelledAtMs);

    Task OutageRemovedAsync(string groupId);

    // Admin migration (Account tab): builds the feed document of EVERY group from the announcements and outages.
    // Safe to repeat. Costs a few reads and one write per group.
    Task<OperationResult<int>> RebuildAllAsync();
}
