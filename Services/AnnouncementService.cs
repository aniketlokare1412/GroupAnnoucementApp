using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IAnnouncementRepository _announcementRepository;
    private readonly IGroupRepository _groupRepository;

    public AnnouncementService(
        ISessionService session,
        IAuthService authService,
        IAnnouncementRepository announcementRepository,
        IGroupRepository groupRepository)
    {
        _session = session;
        _authService = authService;
        _announcementRepository = announcementRepository;
        _groupRepository = groupRepository;
    }

    public async Task<OperationResult<List<Announcement>>> GetForGroupAsync(string groupId)
    {
        if (_session.CurrentProfile == null)
        {
            return OperationResult<List<Announcement>>.Fail("Please sign in again.");
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return OperationResult<List<Announcement>>.Fail("Unknown group.");
        }

        try
        {
            // Members may only run the "active announcements" query (see firestore.rules).
            bool activeOnly = !_session.IsAdmin;

            List<Announcement> items = await ServiceHelper.WithTimeoutAsync(
                _announcementRepository.GetByGroupAsync(groupId, activeOnly));
            items.Sort(CompareNewestFirst);
            return OperationResult<List<Announcement>>.Success(items);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<Announcement>>.Fail("Loading announcements is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ANNOUNCEMENTS ERROR: " + ex);
            return OperationResult<List<Announcement>>.Fail(ServiceHelper.MapError(ex, "We could not load the announcements. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<Announcement?>> GetAsync(string groupId, string announcementId)
    {
        if (GetAdminUid() == null)
        {
            return OperationResult<Announcement?>.Fail("Only administrators can do that.");
        }

        if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(announcementId))
        {
            return OperationResult<Announcement?>.Fail("Unknown announcement.");
        }

        try
        {
            Announcement? announcement = await ServiceHelper.WithTimeoutAsync(
                _announcementRepository.GetByIdAsync(groupId, announcementId));
            if (announcement == null)
            {
                return OperationResult<Announcement?>.Fail("This announcement no longer exists.");
            }

            return OperationResult<Announcement?>.Success(announcement);
        }
        catch (TimeoutException)
        {
            return OperationResult<Announcement?>.Fail("Loading the announcement is taking too long. Please try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ANNOUNCEMENT ERROR: " + ex);
            return OperationResult<Announcement?>.Fail(ServiceHelper.MapError(ex, "We could not load the announcement. Please try again."));
        }
    }

    public async Task<OperationResult<PublishResult>> PublishAsync(string title, string message, List<string> groupIds)
    {
        string? uid = GetAdminUid();
        if (uid == null)
        {
            return OperationResult<PublishResult>.Fail("Only administrators can post announcements.");
        }

        string cleanTitle = (title ?? string.Empty).Trim();
        string cleanMessage = (message ?? string.Empty).Trim();

        string? problem = ValidateText(cleanTitle, cleanMessage);
        if (problem != null)
        {
            return OperationResult<PublishResult>.Fail(problem);
        }

        // Remove blanks and duplicates, keeping the order.
        List<string> targets = new List<string>();
        if (groupIds != null)
        {
            for (int i = 0; i < groupIds.Count; i++)
            {
                string id = groupIds[i];
                if (!string.IsNullOrEmpty(id) && !targets.Contains(id))
                {
                    targets.Add(id);
                }
            }
        }

        if (targets.Count == 0)
        {
            return OperationResult<PublishResult>.Fail("Select at least one group.");
        }

        if (!IsOnline())
        {
            return OperationResult<PublishResult>.Fail("You are offline. Please connect to the internet and try again.");
        }

        try
        {
            // Every target must still be an active group.
            List<AnnouncementGroup> allGroups = await ServiceHelper.WithTimeoutAsync(_groupRepository.GetAllAsync());
            for (int i = 0; i < targets.Count; i++)
            {
                if (!IsActiveGroup(allGroups, targets[i]))
                {
                    return OperationResult<PublishResult>.Fail("One of the selected groups is no longer active. Refresh the list and try again.");
                }
            }

            // Copies posted together share a batch id.
            string batchId = string.Empty;
            if (targets.Count > 1)
            {
                batchId = Guid.NewGuid().ToString("N");
            }

            PublishResult result = new PublishResult();
            string? lastError = null;

            for (int i = 0; i < targets.Count; i++)
            {
                string groupId = targets[i];

                Announcement announcement = new Announcement();
                announcement.GroupId = groupId;
                announcement.BatchId = batchId;
                announcement.Title = cleanTitle;
                announcement.Message = cleanMessage;
                announcement.IsActive = true;
                announcement.IsEdited = false;
                announcement.CreatedBy = uid;

                string announcementId = Guid.NewGuid().ToString("N");

                try
                {
                    await ServiceHelper.WithTimeoutAsync(_announcementRepository.CreateAsync(groupId, announcementId, announcement));
                    result.PostedGroupIds.Add(groupId);
                }
                catch (TimeoutException)
                {
                    result.FailedGroupIds.Add(groupId);
                    lastError = "Some requests took too long and may still go through. Refresh the group's announcements to check.";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("PUBLISH ANNOUNCEMENT ERROR: " + ex);
                    result.FailedGroupIds.Add(groupId);
                    lastError = ServiceHelper.MapError(ex, "We could not post the announcement.");
                }
            }

            if (result.PostedGroupIds.Count == 0)
            {
                return OperationResult<PublishResult>.Fail(lastError ?? "We could not post the announcement. Please try again.");
            }

            result.ErrorMessage = lastError;
            return OperationResult<PublishResult>.Success(result);
        }
        catch (TimeoutException)
        {
            return OperationResult<PublishResult>.Fail("The request is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PUBLISH ANNOUNCEMENT ERROR: " + ex);
            return OperationResult<PublishResult>.Fail(ServiceHelper.MapError(ex, "We could not post the announcement. Please try again."));
        }
    }

    public async Task<OperationResult> UpdateAsync(string groupId, string announcementId, string title, string message)
    {
        string? uid = GetAdminUid();
        if (uid == null)
        {
            return OperationResult.Fail("Only administrators can edit announcements.");
        }

        if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(announcementId))
        {
            return OperationResult.Fail("Unknown announcement.");
        }

        string cleanTitle = (title ?? string.Empty).Trim();
        string cleanMessage = (message ?? string.Empty).Trim();

        string? problem = ValidateText(cleanTitle, cleanMessage);
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        if (!IsOnline())
        {
            return OperationResult.Fail("You are offline. Please connect to the internet and try again.");
        }

        try
        {
            OperationResult? blocked = await CheckCanChangeAsync(groupId, announcementId, uid);
            if (blocked != null)
            {
                return blocked;
            }

            await ServiceHelper.WithTimeoutAsync(_announcementRepository.UpdateAsync(groupId, announcementId, cleanTitle, cleanMessage));
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see whether the change was saved.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE ANNOUNCEMENT ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not save the announcement. Please try again."));
        }
    }

    public async Task<OperationResult> DeleteAsync(string groupId, string announcementId)
    {
        string? uid = GetAdminUid();
        if (uid == null)
        {
            return OperationResult.Fail("Only administrators can delete announcements.");
        }

        if (string.IsNullOrEmpty(groupId) || string.IsNullOrEmpty(announcementId))
        {
            return OperationResult.Fail("Unknown announcement.");
        }

        if (!IsOnline())
        {
            return OperationResult.Fail("You are offline. Please connect to the internet and try again.");
        }

        try
        {
            OperationResult? blocked = await CheckCanChangeAsync(groupId, announcementId, uid);
            if (blocked != null)
            {
                return blocked;
            }

            await ServiceHelper.WithTimeoutAsync(_announcementRepository.SetActiveAsync(groupId, announcementId, false));
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see whether it was deleted.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE ANNOUNCEMENT ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not delete the announcement. Please try again."));
        }
    }

    // Returns a failure when the announcement is missing, already deleted, or posted by
    // another admin. Returns null when the signed-in admin may change it.
    private async Task<OperationResult?> CheckCanChangeAsync(string groupId, string announcementId, string uid)
    {
        Announcement? existing = await ServiceHelper.WithTimeoutAsync(_announcementRepository.GetByIdAsync(groupId, announcementId));
        if (existing == null)
        {
            return OperationResult.Fail("This announcement no longer exists.");
        }

        if (existing.CreatedBy != uid)
        {
            return OperationResult.Fail("Only the administrator who posted this announcement can change it.");
        }

        if (!existing.IsActive)
        {
            return OperationResult.Fail("This announcement has already been deleted.");
        }

        return null;
    }

    private static string? ValidateText(string title, string message)
    {
        if (title.Length == 0)
        {
            return "Enter a title.";
        }

        if (title.Length > AnnouncementLimits.MaxTitleLength)
        {
            return "The title can be at most " + AnnouncementLimits.MaxTitleLength + " characters.";
        }

        if (message.Length == 0)
        {
            return "Enter a message.";
        }

        if (message.Length > AnnouncementLimits.MaxMessageLength)
        {
            return "The message can be at most " + AnnouncementLimits.MaxMessageLength + " characters.";
        }

        return null;
    }

    // The uid of the signed-in ADMIN, or null for everyone else.
    private string? GetAdminUid()
    {
        if (_session.CurrentProfile == null || !_session.IsAdmin)
        {
            return null;
        }

        string? uid = _authService.GetCurrentUserId();
        if (string.IsNullOrEmpty(uid))
        {
            return null;
        }

        return uid;
    }

    private static int CompareNewestFirst(Announcement a, Announcement b)
    {
        return b.CreatedAt.CompareTo(a.CreatedAt);
    }

    private static bool IsActiveGroup(List<AnnouncementGroup> groups, string groupId)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Id == groupId)
            {
                return groups[i].IsActive;
            }
        }

        return false;
    }

    // Without a connection Firestore would queue the write and apply it later,
    // which is confusing for a post or an edit. So we refuse up front.
    private static bool IsOnline()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }
}
