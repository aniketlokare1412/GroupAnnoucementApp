using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Planned outages: an admin schedules one per group (date, from and to time, when to publish) or cancels it.
public interface IOutageService
{
    // Admin: schedules an outage for ONE group (the page calls this for each selected group).
    Task<OperationResult> ScheduleAsync(string groupId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset publishAt);

    // Admin: cancels a scheduled outage that is not over yet.
    Task<OperationResult> CancelAsync(string groupId);

    // Admin: deletes a CANCELLED outage message, so members see the regular announcement again.
    Task<OperationResult> DeleteAsync(string groupId);

    // The group's outage (null when there is none). Members and admins.
    Task<OperationResult<Outage?>> GetAsync(string groupId);
}
