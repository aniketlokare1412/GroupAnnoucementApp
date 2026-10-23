using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class FeedService : IFeedService
{
    private const int MaxParallelRebuilds = 8;

    private readonly ISessionService _session;
    private readonly IGroupFeedRepository _feeds;
    private readonly IAnnouncementRepository _announcements;
    private readonly IOutageRepository _outages;
    private readonly IGroupRepository _groups;

    public FeedService(
        ISessionService session,
        IGroupFeedRepository feeds,
        IAnnouncementRepository announcements,
        IOutageRepository outages,
        IGroupRepository groups)
    {
        _session = session;
        _feeds = feeds;
        _announcements = announcements;
        _outages = outages;
        _groups = groups;
    }

    public async Task PostedAsync(string groupId, Announcement announcement)
    {
        // The outage already in the feed is carried over: a post must not make an outage message disappear.
        GroupFeedDocument? previous = await ServiceHelper.WithTimeoutAsync(_feeds.GetAsync(groupId));
        if (previous == null)
        {
            // No feed yet (a group from before Phase 2): take the outage from its own document.
            Outage? outage = await ServiceHelper.WithTimeoutAsync(_outages.GetAsync(groupId));
            previous = GroupFeedDocument.ForRebuild(groupId, null, outage);
        }

        GroupFeedDocument feed = GroupFeedDocument.ForPost(groupId, announcement, previous);
        await ServiceHelper.WithTimeoutAsync(_feeds.SetAsync(groupId, feed));
    }

    public async Task QueuePostedAsync(FirestoreBatch batch, string groupId, Announcement announcement)
    {
        GroupFeedDocument? previous = await ServiceHelper.WithTimeoutAsync(_feeds.GetAsync(groupId));
        if (previous == null)
        {
            Outage? outage = await ServiceHelper.WithTimeoutAsync(_outages.GetAsync(groupId));
            previous = GroupFeedDocument.ForRebuild(groupId, null, outage);
        }

        _feeds.QueueSet(batch, groupId, GroupFeedDocument.ForPost(groupId, announcement, previous));
    }

    public Task AnnouncementEditedAsync(string groupId, string title, string message)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("aTitle", (object?)title),
            ("aMessage", (object?)message),
            ("aEdited", (object?)true));
    }

    public Task AnnouncementCompletedAsync(string groupId, long completedAtMs)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("aStatus", (object?)AnnouncementStatus.Completed),
            ("aCompletedAtMs", (object?)completedAtMs));
    }

    public Task AnnouncementRemovedAsync(string groupId)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("aId", (object?)string.Empty),
            ("aTitle", (object?)string.Empty),
            ("aMessage", (object?)string.Empty),
            ("aStatus", (object?)string.Empty),
            ("aResolveMinutes", (object?)0),
            ("aCompletedAtMs", (object?)0L),
            ("aAttachmentName", (object?)string.Empty),
            ("aAttachmentKind", (object?)string.Empty),
            ("aFileId", (object?)string.Empty),
            ("aEdited", (object?)false),
            ("aAtMs", (object?)0L));
    }

    public Task OutageScheduledAsync(string groupId, Outage outage)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("oStatus", (object?)outage.Status),
            ("oStartMs", (object?)outage.StartMs),
            ("oEndMs", (object?)outage.EndMs),
            ("oPublishAtMs", (object?)outage.PublishAtMs),
            ("oCancelledAtMs", (object?)outage.CancelledAtMs));
    }

    public Task OutageCancelledAsync(string groupId, long cancelledAtMs)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("oStatus", (object?)OutageText.Cancelled),
            ("oCancelledAtMs", (object?)cancelledAtMs));
    }

    public Task OutageRemovedAsync(string groupId)
    {
        return UpdateOrRebuildAsync(
            groupId,
            ("oStatus", (object?)string.Empty),
            ("oStartMs", (object?)0L),
            ("oEndMs", (object?)0L),
            ("oPublishAtMs", (object?)0L),
            ("oCancelledAtMs", (object?)0L));
    }

    public async Task<OperationResult<int>> RebuildAllAsync()
    {
        if (_session.CurrentProfile == null || !_session.IsAdmin)
        {
            return OperationResult<int>.Fail("Only administrators can rebuild the feeds.");
        }

        try
        {
            List<AnnouncementGroup> groups = await ServiceHelper.WithTimeoutAsync(_groups.GetAllAsync());

            int rebuilt = 0;
            using (SemaphoreSlim gate = new SemaphoreSlim(MaxParallelRebuilds))
            {
                List<Task> jobs = new List<Task>();
                for (int i = 0; i < groups.Count; i++)
                {
                    string groupId = groups[i].Id;
                    jobs.Add(RunLimitedAsync(gate, async () =>
                    {
                        await RebuildGroupAsync(groupId);
                        Interlocked.Increment(ref rebuilt);
                    }));
                }

                await Task.WhenAll(jobs);
            }

            return OperationResult<int>.Success(rebuilt);
        }
        catch (TimeoutException)
        {
            return OperationResult<int>.Fail("The request is taking too long. Please try again (rebuilding is safe to repeat).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REBUILD FEEDS ERROR: " + ex);
            return OperationResult<int>.Fail(ServiceHelper.MapError(ex, "We could not rebuild the feeds. Please try again."));
        }
    }

    // Changes single fields of the feed. A group without a feed yet is built from its announcements and outage
    // instead (which already contain the change that was just saved).
    private async Task UpdateOrRebuildAsync(string groupId, params (string, object?)[] fields)
    {
        try
        {
            await ServiceHelper.WithTimeoutAsync(_feeds.UpdateAsync(groupId, fields));
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            await RebuildGroupAsync(groupId);
        }
    }

    private async Task RebuildGroupAsync(string groupId)
    {
        Task<List<Announcement>> announcementsTask = ServiceHelper.WithTimeoutAsync(_announcements.GetByGroupAsync(groupId, true));
        Task<Outage?> outageTask = ServiceHelper.WithTimeoutAsync(_outages.GetAsync(groupId));
        await Task.WhenAll(announcementsTask, outageTask);

        Announcement? latest = Newest(announcementsTask.Result);
        GroupFeedDocument feed = GroupFeedDocument.ForRebuild(groupId, latest, outageTask.Result);
        await ServiceHelper.WithTimeoutAsync(_feeds.SetAsync(groupId, feed));
    }

    // Only the newest active announcement counts (older ones are normally deleted already).
    private static Announcement? Newest(List<Announcement> items)
    {
        Announcement? newest = null;
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].IsActive)
            {
                continue;
            }

            if (newest == null || items[i].CreatedAt > newest.CreatedAt)
            {
                newest = items[i];
            }
        }

        return newest;
    }

    private static bool IsNotFound(Exception ex)
    {
        string text = (ex.GetType().FullName + " " + ex.Message).ToLowerInvariant();
        return text.Contains("not_found") || text.Contains("not found") || text.Contains("no document to update");
    }

    private static async Task RunLimitedAsync(SemaphoreSlim gate, Func<Task> work)
    {
        await gate.WaitAsync();
        try
        {
            await work();
        }
        finally
        {
            gate.Release();
        }
    }
}
