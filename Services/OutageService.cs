using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class OutageService : IOutageService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IOutageRepository _outageRepository;

    public OutageService(ISessionService session, IAuthService authService, IOutageRepository outageRepository)
    {
        _session = session;
        _authService = authService;
        _outageRepository = outageRepository;
    }

    public async Task<OperationResult> ScheduleAsync(string groupId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset publishAt)
    {
        string? uid = _authService.GetCurrentUserId();
        if (uid == null || !_session.IsAdmin)
        {
            return OperationResult.Fail("Only administrators can schedule an outage.");
        }

        if (end <= start)
        {
            return OperationResult.Fail("The outage must end after it starts.");
        }

        if (publishAt > start)
        {
            return OperationResult.Fail("The message must be published before the outage starts.");
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            // One outage per group at a time: the old one must be cancelled (or over) first.
            Outage? existing = await ServiceHelper.WithTimeoutAsync(_outageRepository.GetAsync(groupId));
            if (existing != null && existing.Status == OutageText.Scheduled && existing.EndMs > nowMs)
            {
                return OperationResult.Fail("This group already has a planned outage. Cancel it first.");
            }

            Outage outage = new Outage();
            outage.GroupId = groupId;
            outage.Status = OutageText.Scheduled;
            outage.StartMs = start.ToUnixTimeMilliseconds();
            outage.EndMs = end.ToUnixTimeMilliseconds();
            outage.PublishAtMs = publishAt.ToUnixTimeMilliseconds();
            outage.CancelledAtMs = 0;
            outage.CreatedBy = uid;

            await ServiceHelper.WithTimeoutAsync(_outageRepository.SetAsync(groupId, outage));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SCHEDULE OUTAGE ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not schedule the outage. Please try again."));
        }
    }

    public async Task<OperationResult> CancelAsync(string groupId)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult.Fail("Only administrators can cancel an outage.");
        }

        try
        {
            Outage? existing = await ServiceHelper.WithTimeoutAsync(_outageRepository.GetAsync(groupId));
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (existing == null || existing.Status != OutageText.Scheduled || existing.EndMs <= nowMs)
            {
                return OperationResult.Fail("There is no planned outage to cancel.");
            }

            await ServiceHelper.WithTimeoutAsync(_outageRepository.CancelAsync(groupId, nowMs));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CANCEL OUTAGE ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not cancel the outage. Please try again."));
        }
    }

    public async Task<OperationResult<Outage?>> GetAsync(string groupId)
    {
        try
        {
            Outage? outage = await ServiceHelper.WithTimeoutAsync(_outageRepository.GetAsync(groupId));
            return OperationResult<Outage?>.Success(outage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD OUTAGE ERROR: " + ex);
            return OperationResult<Outage?>.Fail(ServiceHelper.MapError(ex, "We could not load the outage."));
        }
    }
}
