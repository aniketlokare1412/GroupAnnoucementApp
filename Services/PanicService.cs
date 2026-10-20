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

    public PanicService(
        IAuthService authService,
        IPanicRepository panicRepository,
        IAnnouncementRepository announcementRepository)
    {
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
            DateTimeOffset? last = await ServiceHelper.WithTimeoutAsync(_panicRepository.LastPressAsync(groupId, announcementId, uid));
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
        List<Task<GroupActivity>> tasks = new List<Task<GroupActivity>>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            tasks.Add(LatestActivityAsync(groupIds[i]));
        }

        await Task.WhenAll(tasks);

        Dictionary<string, GroupActivity> result = new Dictionary<string, GroupActivity>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            result[groupIds[i]] = tasks[i].Result;
        }

        return result;
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

            if (latest == null)
            {
                return activity;
            }

            activity.LastPostedAt = latest.CreatedAt == default ? DateTimeOffset.UtcNow : latest.CreatedAt;
            activity.IsCompleted = latest.Status == AnnouncementStatus.Completed;
            activity.PanicCount = await ServiceHelper.WithTimeoutAsync(_panicRepository.CountAsync(groupId, latest.Id));
            return activity;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LATEST ACTIVITY ERROR: " + ex);
            return activity;
        }
    }
}
