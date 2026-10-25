using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class PanicService : IPanicService
{
    private readonly IAuthService _authService;
    private readonly IPanicRepository _panicRepository;
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IGroupStatsService _statsService;
    private readonly IAdminStatsWatcher _statsWatcher;

    // Groups that had no stats document: worked out the old way once and remembered for this session.
    private readonly Dictionary<string, GroupActivity> _repaired = new Dictionary<string, GroupActivity>();

    public PanicService(
        IAuthService authService,
        IPanicRepository panicRepository,
        IAnnouncementRepository announcementRepository,
        IGroupStatsService statsService,
        IAdminStatsWatcher statsWatcher)
    {
        _statsService = statsService;
        _statsWatcher = statsWatcher;
        _authService = authService;
        _panicRepository = panicRepository;
        _announcementRepository = announcementRepository;
    }

    public async Task<OperationResult> PressAsync(string groupId, string announcementId)
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null)
        {
            return OperationResult.Fail("Please sign in again.");
        }

        try
        {
            // The same 5-minute wait as on the button, so it cannot be skipped.
            DateTimeOffset? last = await ServiceHelper.WithTimeoutAsync(_panicRepository.LastPressAsync(groupId, announcementId, uid));
            if (last != null && DateTimeOffset.UtcNow - last.Value < TimeSpan.FromMinutes(PanicLimits.CooldownMinutes))
            {
                return OperationResult.Fail("You can report again " + PanicLimits.CooldownMinutes + " minutes after your last report.");
            }

            await ServiceHelper.WithTimeoutAsync(_panicRepository.CreateAsync(groupId, announcementId, uid));

            // Phase 3: the press is remembered on this phone (for the 5-minute wait on the card) and added to the
            // group's counter that the admin screens read. The counter is best effort and never fails the press.
            PanicMemory.Set(uid, groupId, announcementId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _ = _statsService.PanicPressedAsync(groupId, announcementId);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not send your report. It may not be open yet."));
        }
    }

    public async Task<OperationResult<bool>> HasPressedAsync(string groupId, string announcementId)
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null)
        {
            return OperationResult<bool>.Success(false);
        }

        try
        {
            bool exists = await ServiceHelper.WithTimeoutAsync(_panicRepository.ExistsAsync(groupId, announcementId, uid));
            return OperationResult<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC CHECK ERROR: " + ex);
            return OperationResult<bool>.Fail(ServiceHelper.MapError(ex, "We could not check your report."));
        }
    }

    public async Task<OperationResult<DateTimeOffset?>> GetLastPressAsync(string groupId, string announcementId)
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null)
        {
            return OperationResult<DateTimeOffset?>.Success(null);
        }

        try
        {
            // Phase 3: this phone usually knows already (it asked once for this announcement): no read.
            long knownMs;
            if (PanicMemory.TryGet(uid, groupId, announcementId, out knownMs))
            {
                return OperationResult<DateTimeOffset?>.Success(knownMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(knownMs) : (DateTimeOffset?)null);
            }

            DateTimeOffset? last = await ServiceHelper.WithTimeoutAsync(_panicRepository.LastPressAsync(groupId, announcementId, uid));
            PanicMemory.Set(uid, groupId, announcementId, last == null ? 0 : last.Value.ToUnixTimeMilliseconds());
            return OperationResult<DateTimeOffset?>.Success(last);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC LAST PRESS ERROR: " + ex);
            return OperationResult<DateTimeOffset?>.Fail(ServiceHelper.MapError(ex, "We could not check your last report."));
        }
    }

    public async Task<OperationResult<int>> CountAsync(string groupId, string announcementId)
    {
        try
        {
            int count = await ServiceHelper.WithTimeoutAsync(_panicRepository.CountAsync(groupId, announcementId));
            return OperationResult<int>.Success(count);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC COUNT ERROR: " + ex);
            return OperationResult<int>.Fail(ServiceHelper.MapError(ex, "We could not load the report count."));
        }
    }

    public async Task<Dictionary<string, int>> GetLatestCountsAsync(List<string> groupIds)
    {
        Dictionary<string, GroupActivity> activity = await GetLatestActivityAsync(groupIds);

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            counts[groupIds[i]] = activity[groupIds[i]].PanicCount;
        }

        return counts;
    }

    public async Task<Dictionary<string, GroupActivity>> GetLatestActivityAsync(List<string> groupIds)
    {
        // Phase 3: the live stats listener already holds every group's numbers (no reads). Before the new rules
        // are deployed it cannot start, and the old way below is used.
        bool live = false;
        try
        {
            live = await _statsWatcher.EnsureStartedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("STATS LIVE START ERROR: " + ex);
        }

        if (!live)
        {
            return await GetLatestActivityOldWayAsync(groupIds);
        }

        Dictionary<string, GroupActivity> known = _statsWatcher.Snapshot();
        Dictionary<string, GroupActivity> result = new Dictionary<string, GroupActivity>();
        List<string> missing = new List<string>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            GroupActivity? item;
            if (known.TryGetValue(groupIds[i], out item))
            {
                result[groupIds[i]] = item;
            }
            else if (_repaired.TryGetValue(groupIds[i], out item))
            {
                result[groupIds[i]] = item;
            }
            else
            {
                missing.Add(groupIds[i]);
            }
        }

        // A group without a stats document (not rebuilt yet): worked out the old way once and the document is
        // created for next time. Every group is in here only until "Rebuild stats" has been run (or it was posted to).
        if (missing.Count > 0)
        {
            Dictionary<string, GroupActivity> computed = await GetLatestActivityOldWayAsync(missing);
            for (int i = 0; i < missing.Count; i++)
            {
                result[missing[i]] = computed[missing[i]];
                _repaired[missing[i]] = computed[missing[i]];
                _ = _statsService.RepairGroupAsync(missing[i]);
            }
        }

        return result;
    }

    private async Task<Dictionary<string, GroupActivity>> GetLatestActivityOldWayAsync(List<string> groupIds)
    {
        // With 200 groups this must not start 400 requests at once: at most 10 groups are read at a time.
        List<Task<GroupActivity>> tasks = new List<Task<GroupActivity>>();
        using (SemaphoreSlim gate = new SemaphoreSlim(10))
        {
            for (int i = 0; i < groupIds.Count; i++)
            {
                string groupId = groupIds[i];
                tasks.Add(LimitedActivityAsync(gate, groupId));
            }

            await Task.WhenAll(tasks);
        }

        Dictionary<string, GroupActivity> result = new Dictionary<string, GroupActivity>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            result[groupIds[i]] = tasks[i].Result;
        }

        return result;
    }

    private async Task<GroupActivity> LimitedActivityAsync(SemaphoreSlim gate, string groupId)
    {
        await gate.WaitAsync();
        try
        {
            return await LatestActivityAsync(groupId);
        }
        finally
        {
            gate.Release();
        }
    }

    // The newest announcement of one group: its panic count, when it was posted and whether it is completed.
    // Any problem counts as "nothing".
    private async Task<GroupActivity> LatestActivityAsync(string groupId)
    {
        GroupActivity activity = new GroupActivity();
        try
        {
            List<Announcement> all = await ServiceHelper.WithTimeoutAsync(_announcementRepository.GetByGroupAsync(groupId, false));
            Announcement? latest = null;
            for (int i = 0; i < all.Count; i++)
            {
                // A just-posted announcement can still have no server time: it is the newest.
                if (latest == null || all[i].CreatedAt == default || (latest.CreatedAt != default && all[i].CreatedAt > latest.CreatedAt))
                {
                    latest = all[i];
                }
            }

            // With no open announcement the members see the 5-minute notice: its panic presses count.
            if (latest == null)
            {
                activity.PanicCount = await ServiceHelper.WithTimeoutAsync(_panicRepository.CountAsync(groupId, PanicLimits.NoticeAnnouncementId));
                return activity;
            }

            activity.LastPostedAt = latest.CreatedAt == default ? DateTimeOffset.UtcNow : latest.CreatedAt;
            activity.IsCompleted = latest.Status == AnnouncementStatus.Completed;
            string countedId = activity.IsCompleted ? PanicLimits.NoticeAnnouncementId : latest.Id;
            activity.PanicCount = await ServiceHelper.WithTimeoutAsync(_panicRepository.CountAsync(groupId, countedId));
            return activity;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LATEST ACTIVITY ERROR: " + ex);
            return activity;
        }
    }
}
