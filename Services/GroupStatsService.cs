using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class GroupStatsService : IGroupStatsService
{
    private const int MaxParallelRebuilds = 8;

    private readonly ISessionService _session;
    private readonly IGroupStatsRepository _stats;
    private readonly IAnnouncementRepository _announcements;
    private readonly IPanicRepository _panics;
    private readonly IGroupRepository _groups;

    public GroupStatsService(
        ISessionService session,
        IGroupStatsRepository stats,
        IAnnouncementRepository announcements,
        IPanicRepository panics,
        IGroupRepository groups)
    {
        _session = session;
        _stats = stats;
        _announcements = announcements;
        _panics = panics;
        _groups = groups;
    }

    public async Task PostedAsync(string groupId)
    {
        try
        {
            // The whole document is replaced: the new message has no reports and the notice reports are cleared
            // (posting clears them in the press documents too).
            GroupStatsDocument stats = GroupStatsDocument.ForPost(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await ServiceHelper.WithTimeoutAsync(_stats.SetAsync(groupId, stats));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("STATS POSTED ERROR: " + ex);
        }
    }

    public void QueuePosted(FirestoreBatch batch, string groupId)
    {
        _stats.QueueSet(batch, groupId, GroupStatsDocument.ForPost(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    public async Task CompletedAsync(string groupId)
    {
        try
        {
            await ServiceHelper.WithTimeoutAsync(_stats.UpdateAsync(groupId, ("isCompleted", (object?)true)));
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            // No stats yet: build them from the data (which already contains the completion).
            await RepairGroupAsync(groupId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("STATS COMPLETED ERROR: " + ex);
        }
    }

    public async Task RemovedAsync(string groupId)
    {
        try
        {
            // "Last posted" stays, so the group keeps its place in the "most recently posted" order.
            await ServiceHelper.WithTimeoutAsync(_stats.UpdateAsync(
                groupId,
                ("hasAnnouncement", (object?)false),
                ("isCompleted", (object?)false),
                ("panicCount", (object?)0)));
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            await RepairGroupAsync(groupId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("STATS REMOVED ERROR: " + ex);
        }
    }

    public async Task PanicPressedAsync(string groupId, string announcementId)
    {
        try
        {
            bool onNotice = string.Equals(announcementId, PanicLimits.NoticeAnnouncementId, StringComparison.Ordinal);
            await ServiceHelper.WithTimeoutAsync(_stats.IncrementPanicAsync(groupId, onNotice));
        }
        catch (Exception ex)
        {
            // The press itself is saved. A missed count is repaired by "Rebuild stats".
            System.Diagnostics.Debug.WriteLine("STATS PANIC INCREMENT ERROR: " + ex);
        }
    }

    public async Task<GroupStatsDocument?> RepairGroupAsync(string groupId)
    {
        try
        {
            GroupStatsDocument stats = await ComputeAsync(groupId);
            await ServiceHelper.WithTimeoutAsync(_stats.SetAsync(groupId, stats));
            return stats;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("STATS REPAIR ERROR: " + ex);
            return null;
        }
    }

    public async Task<OperationResult<int>> RebuildAllAsync()
    {
        if (_session.CurrentProfile == null || !_session.IsAdmin)
        {
            return OperationResult<int>.Fail("Only administrators can rebuild the stats.");
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
                        // Unlike a repair, a failure here is NOT hidden: wrong zeros must not be written.
                        GroupStatsDocument stats = await ComputeAsync(groupId);
                        await ServiceHelper.WithTimeoutAsync(_stats.SetAsync(groupId, stats));
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
            System.Diagnostics.Debug.WriteLine("REBUILD STATS ERROR: " + ex);
            return OperationResult<int>.Fail(ServiceHelper.MapError(ex, "We could not rebuild the stats. Please try again."));
        }
    }

    // The stats of one group worked out from the real data. Throws on any problem.
    //   open message, not completed -> its press count; the notice has none (posting clears them)
    //   completed or no message     -> the notice's press count
    private async Task<GroupStatsDocument> ComputeAsync(string groupId)
    {
        List<Announcement> all = await ServiceHelper.WithTimeoutAsync(_announcements.GetByGroupAsync(groupId, true));
        Announcement? latest = Newest(all);

        GroupStatsDocument stats = new GroupStatsDocument();
        if (latest == null)
        {
            stats.NoticePanicCount = await ServiceHelper.WithTimeoutAsync(_panics.CountAsync(groupId, PanicLimits.NoticeAnnouncementId));
            return stats;
        }

        stats.HasAnnouncement = true;
        stats.LastPostedAtMs = (latest.CreatedAt == default ? DateTimeOffset.UtcNow : latest.CreatedAt).ToUnixTimeMilliseconds();
        stats.IsCompleted = latest.Status == AnnouncementStatus.Completed;
        if (stats.IsCompleted)
        {
            stats.NoticePanicCount = await ServiceHelper.WithTimeoutAsync(_panics.CountAsync(groupId, PanicLimits.NoticeAnnouncementId));
        }
        else
        {
            stats.PanicCount = await ServiceHelper.WithTimeoutAsync(_panics.CountAsync(groupId, latest.Id));
        }

        return stats;
    }

    private static Announcement? Newest(List<Announcement> items)
    {
        Announcement? newest = null;
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].IsActive)
            {
                continue;
            }

            // A just-posted announcement can still have no server time: it is the newest.
            if (newest == null || items[i].CreatedAt == default || (newest.CreatedAt != default && items[i].CreatedAt > newest.CreatedAt))
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
