using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IOutageRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.
    Task SetAsync(string groupId, Outage outage);

    // Returns null when the group has no outage.
    Task<Outage?> GetAsync(string groupId);

    Task CancelAsync(string groupId, long cancelledAtMs);
}
