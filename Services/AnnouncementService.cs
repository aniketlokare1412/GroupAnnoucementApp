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
    private readonly IAttachmentService _attachments;
    private readonly IPanicRepository _panics;
    private readonly IGroupRepository _groupRepository;

    public AnnouncementService(
        ISessionService session,
        IAuthService authService,
        IAnnouncementRepository announcementRepository,
        IGroupRepository groupRepository,
        IAttachmentService attachments,
        IPanicRepository panics)
    {
        _panics = panics;
        _attachments = attachments;
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

            // Only the latest announcement of a group is shown.
            if (items.Count > 1)
            {
                Announcement latest = items[0];
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].IsActive)
                    {
                        latest = items[i];
                        break;
                    }
                }

                items.Clear();
                items.Add(latest);
            }
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

    public async Task<OperationResult<PublishResult>> PublishAsync(string title, string message, List<string> groupIds, AttachmentPick? attachment = null, int resolveMinutes = 0)
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
            List<AnnouncementGroup> allGroups = await LoadTargetGroupsAsync(targets);
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

            // The groups are posted to side by side (a few at a time) instead of one after the other.
            // The results are read back in the order of the selected groups.
            using (SemaphoreSlim gate = new SemaphoreSlim(MaxParallelGroups))
            {
                List<Task<GroupPostOutcome>> posts = new List<Task<GroupPostOutcome>>();
                for (int i = 0; i < targets.Count; i++)
                {
                    string target = targets[i];
                    posts.Add(RunLimitedAsync(gate, () => PostToGroupAsync(target, uid, batchId, cleanTitle, cleanMessage, attachment, resolveMinutes)));
                }

                GroupPostOutcome[] outcomes = await Task.WhenAll(posts);
                for (int i = 0; i < outcomes.Length; i++)
                {
                    if (outcomes[i].Posted)
                    {
                        result.PostedGroupIds.Add(outcomes[i].GroupId);
                    }
                    else
                    {
                        result.FailedGroupIds.Add(outcomes[i].GroupId);
                        lastError = outcomes[i].Error;
                    }
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
    public async Task<OperationResult> MarkCompletedAsync(string groupId, string announcementId)
    {
        if (_session.CurrentProfile == null || !_session.IsAdmin)
        {
            return OperationResult.Fail("Only administrators can mark work as completed.");
        }

        try
        {
            await ServiceHelper.WithTimeoutAsync(_announcementRepository.SetStatusAsync(groupId, announcementId, AnnouncementStatus.Completed, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MARK COMPLETED ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not mark it as completed. Please try again."));
        }
    }

    // How many groups are posted to at the same time.
    private const int MaxParallelGroups = 10;

    // Up to this many selected groups are checked one by one (in parallel); beyond it one read of all groups is cheaper.
    private const int ParallelLookupLimit = 15;

    private sealed class GroupPostOutcome
    {
        public string GroupId = string.Empty;
        public bool Posted;
        public string? Error;
    }

    private static async Task<GroupPostOutcome> RunLimitedAsync(SemaphoreSlim gate, Func<Task<GroupPostOutcome>> work)
    {
        await gate.WaitAsync();
        try
        {
            return await work();
        }
        finally
        {
            gate.Release();
        }
    }

    // The selected groups only. With a few groups this is far cheaper than reading every group.
    private async Task<List<AnnouncementGroup>> LoadTargetGroupsAsync(List<string> targets)
    {
        if (targets.Count > ParallelLookupLimit)
        {
            return await ServiceHelper.WithTimeoutAsync(_groupRepository.GetAllAsync());
        }

        List<Task<AnnouncementGroup?>> lookups = new List<Task<AnnouncementGroup?>>();
        for (int i = 0; i < targets.Count; i++)
        {
            lookups.Add(ServiceHelper.WithTimeoutAsync(_groupRepository.GetByIdAsync(targets[i])));
        }

        await Task.WhenAll(lookups);

        List<AnnouncementGroup> found = new List<AnnouncementGroup>();
        for (int i = 0; i < lookups.Count; i++)
        {
            AnnouncementGroup? group = lookups[i].Result;
            if (group != null)
            {
                found.Add(group);
            }
        }

        return found;
    }

    // Posts to ONE group: file copy, the new announcement, then the removal of the older ones.
    // Never throws: the outcome says whether the announcement was posted.
    private async Task<GroupPostOutcome> PostToGroupAsync(string groupId, string uid, string batchId, string title, string message, AttachmentPick? attachment, int resolveMinutes)
    {
        GroupPostOutcome outcome = new GroupPostOutcome();
        outcome.GroupId = groupId;

        Announcement announcement = new Announcement();
        announcement.GroupId = groupId;
        announcement.BatchId = batchId;
        announcement.Title = title;
        announcement.Message = message;
        announcement.IsActive = true;
        announcement.IsEdited = false;
        announcement.CreatedBy = uid;

        string announcementId = Guid.NewGuid().ToString("N");

        try
        {
            announcement.Status = AnnouncementStatus.Active;
            announcement.ResolveMinutes = resolveMinutes < 0 ? 0 : resolveMinutes;

            // Each group gets its own copy of the file, so replacing one group's
            // announcement never removes a file another group still shows.
            if (attachment != null)
            {
                OperationResult<UploadedAttachment> uploaded = await _attachments.UploadAsync(groupId, announcementId, attachment);
                if (!uploaded.IsSuccess || uploaded.Value == null)
                {
                    throw new InvalidOperationException(uploaded.ErrorMessage ?? "The file could not be saved.");
                }

                announcement.AttachmentName = uploaded.Value.Name;
                announcement.AttachmentKind = uploaded.Value.Kind;
            }

            // The group's current announcements are read while the new one is written (one round trip saved).
            Task<List<Announcement>> olderTask = ReadAnnouncementsQuietlyAsync(groupId);

            await ServiceHelper.WithTimeoutAsync(_announcementRepository.CreateAsync(groupId, announcementId, announcement));
            outcome.Posted = true;

            // A new message replaces the 5-minute notice: its panic presses are cleared (together with the old announcement).
            Task noticeTask = ClearNoticePanicsAsync(groupId);
            await DeleteOlderAsync(groupId, announcementId, olderTask);
            await noticeTask;
        }
        catch (TimeoutException)
        {
            outcome.Error = "Some requests took too long and may still go through. Refresh the group's announcements to check.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PUBLISH ANNOUNCEMENT ERROR: " + ex);
            outcome.Error = ServiceHelper.MapError(ex, "We could not post the announcement.");
        }

        return outcome;
    }

    // Best effort, never throws.
    private async Task ClearNoticePanicsAsync(string groupId)
    {
        try
        {
            await _panics.DeleteAllAsync(groupId, PanicLimits.NoticeAnnouncementId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CLEAR NOTICE PANICS ERROR: " + ex);
        }
    }

    // A failed read counts as "nothing to clean up".
    private async Task<List<Announcement>> ReadAnnouncementsQuietlyAsync(string groupId)
    {
        try
        {
            return await ServiceHelper.WithTimeoutAsync(_announcementRepository.GetByGroupAsync(groupId, false));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("READ OLDER ANNOUNCEMENTS ERROR: " + ex);
            return new List<Announcement>();
        }
    }

    // Only the newest announcement of a group is kept: older ones are deleted for good.
    // This is best effort. If it fails, the list still shows only the latest one.
    private async Task DeleteOlderAsync(string groupId, string keepId, Task<List<Announcement>> olderTask)
    {
        try
        {
            List<Announcement> all = await olderTask;
            List<Task> deletions = new List<Task>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id != keepId)
                {
                    deletions.Add(DeleteOneOlderAsync(groupId, all[i]));
                }
            }

            await Task.WhenAll(deletions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE OLDER ANNOUNCEMENTS ERROR: " + ex);
        }
    }

    // The panic reports and the file go first (together), the announcement itself last.
    private async Task DeleteOneOlderAsync(string groupId, Announcement older)
    {
        Task panicTask = _panics.DeleteAllAsync(groupId, older.Id);
        Task fileTask = string.IsNullOrEmpty(older.AttachmentKind)
            ? Task.CompletedTask
            : _attachments.DeleteAsync(groupId, older.Id);

        await Task.WhenAll(panicTask, fileTask);
        await ServiceHelper.WithTimeoutAsync(_announcementRepository.DeleteAsync(groupId, older.Id));
    }

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
        // A just-posted announcement can still have no server time on this device: it is the newest.
        if (a.CreatedAt == default && b.CreatedAt != default)
        {
            return -1;
        }

        if (b.CreatedAt == default && a.CreatedAt != default)
        {
            return 1;
        }

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
