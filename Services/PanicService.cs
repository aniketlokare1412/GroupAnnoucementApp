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
            await ServiceHelper.WithTimeoutAsync(_panicRepository.CreateAsync(groupId, announcementId, uid));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PANIC ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not send your report. It may not be open yet, or you may have reported already."));
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
        List<Task<int>> tasks = new List<Task<int>>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            tasks.Add(LatestCountAsync(groupIds[i]));
        }

        await Task.WhenAll(tasks);

        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            counts[groupIds[i]] = tasks[i].Result;
        }

        return counts;
    }

    // The count for the newest announcement of one group. Any problem counts as 0.
    private async Task<int> LatestCountAsync(string groupId)
    {
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
                return 0;
            }

            return await ServiceHelper.WithTimeoutAsync(_panicRepository.CountAsync(groupId, latest.Id));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LATEST PANIC COUNT ERROR: " + ex);
            return 0;
        }
    }
}
