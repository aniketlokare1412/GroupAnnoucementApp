using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using System.Collections.Concurrent;
using GroupAnnouncementApp.Repositories;
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
    private readonly IFeedService _feed;
    private readonly IGroupStatsService _stats;
    private readonly IFirestoreBatchFactory _batches;
    private readonly IAdminStatsWatcher _watcher;

    public AnnouncementService(
        ISessionService session,
        IAuthService authService,
        IAnnouncementRepository announcementRepository,
        IGroupRepository groupRepository,
        IAttachmentService attachments,
        IPanicRepository panics,
        IFeedService feed,
        IGroupStatsService stats,
        IFirestoreBatchFactory batches,
        IAdminStatsWatcher watcher)
    {
        _batches = batches;
        _watcher = watcher;
        _feed = feed;
        _stats = stats;
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
            return OperationResult<List<Announcement>>.Fail("Unknown DTC.");
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
            return OperationResult<PublishResult>.Fail("Select at least one DTC.");
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
                    return OperationResult<PublishResult>.Fail("One of the selected DTCs is no longer active. Refresh the list and try again.");
                }
            }

            // Copies posted together share a batch id.
            string batchId = string.Empty;
            if (targets.Count > 1)
            {
                batchId = Guid.NewGuid().ToString("N");
            }

            // Phase 4: a file is stored ONCE for the whole broadcast (every group's announcement points at it).
            PostSettings settings = new PostSettings();
            settings.Uid = uid;
            settings.BatchId = batchId;
            settings.Title = cleanTitle;
            settings.Message = cleanMessage;
            settings.ResolveMinutes = resolveMinutes < 0 ? 0 : resolveMinutes;
            if (attachment != null)
            {
                string fileId = Guid.NewGuid().ToString("N");
                OperationResult<UploadedAttachment> uploaded = await _attachments.UploadSharedAsync(fileId, attachment);
                if (!uploaded.IsSuccess || uploaded.Value == null)
                {
                    return OperationResult<PublishResult>.Fail(uploaded.ErrorMessage ?? "The file could not be saved.");
                }

                settings.SharedFileId = fileId;
                settings.AttachmentName = uploaded.Value.Name;
                settings.AttachmentKind = uploaded.Value.Kind;
            }

            // What the admin Home already knows (live stats): lets the clean-up skip reads that cannot find anything.
            if (_watcher.IsLive)
            {
                settings.Known = _watcher.Snapshot();
            }

            PublishResult result = new PublishResult();
            string? lastError = null;

            // The groups are sent in small batches (12 groups, a few batches at a time). One batch = the announcement,
            // the feed and the stats of each of its groups. The results are read back in the order of the selected groups.
            using (SemaphoreSlim gate = new SemaphoreSlim(MaxParallelBatches))
            {
                List<Task<List<GroupPostOutcome>>> chunks = new List<Task<List<GroupPostOutcome>>>();
                for (int start = 0; start < targets.Count; start += BatchGroupCount)
                {
                    List<string> chunk = targets.GetRange(start, Math.Min(BatchGroupCount, targets.Count - start));
                    chunks.Add(RunLimitedAsync(gate, () => PostChunkAsync(chunk, settings)));
                }

                List<GroupPostOutcome>[] outcomes = await Task.WhenAll(chunks);
                for (int c = 0; c < outcomes.Length; c++)
                {
                    for (int i = 0; i < outcomes[c].Count; i++)
                    {
                        if (outcomes[c][i].Posted)
                        {
                            result.PostedGroupIds.Add(outcomes[c][i].GroupId);
                        }
                        else
                        {
                            result.FailedGroupIds.Add(outcomes[c][i].GroupId);
                            lastError = outcomes[c][i].Error;
                        }
                    }
                }
            }

            // Shared files of the replaced announcements are removed when no announcement uses them any more.
            await SweepSharedFilesAsync(settings.ReplacedFiles);

            // Nothing was posted: the file just stored is not used by anyone.
            if (result.PostedGroupIds.Count == 0 && !string.IsNullOrEmpty(settings.SharedFileId))
            {
                await _attachments.DeleteSharedAsync(settings.SharedFileId);
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

            // Phase 2: members listen to the group's feed document, so it gets the change too.
            await _feed.AnnouncementEditedAsync(groupId, cleanTitle, cleanMessage);
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
            await _feed.AnnouncementRemovedAsync(groupId);
            await _stats.RemovedAsync(groupId);
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
            long completedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await ServiceHelper.WithTimeoutAsync(_announcementRepository.SetStatusAsync(groupId, announcementId, AnnouncementStatus.Completed, completedAtMs));
            await _feed.AnnouncementCompletedAsync(groupId, completedAtMs);
            await _stats.CompletedAsync(groupId);
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

    // Phase 4: groups per batch, and batches sent at the same time. A batch may hold 500 writes, but security
    // rules may look at about 20 other documents per batch (one group document per group), so 12 groups is safe.
    private const int BatchGroupCount = 12;
    private const int MaxParallelBatches = 4;

    // Up to this many selected groups are checked one by one (in parallel); beyond it one read of all groups is cheaper.
    private const int ParallelLookupLimit = 15;

    private sealed class GroupPostOutcome
    {
        public string GroupId = string.Empty;
        public bool Posted;
        public string? Error;
    }

    private static async Task<T> RunLimitedAsync<T>(SemaphoreSlim gate, Func<Task<T>> work)
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

    // Everything the groups of one post share.
    private sealed class PostSettings
    {
        public string Uid = string.Empty;
        public string BatchId = string.Empty;
        public string Title = string.Empty;
        public string Message = string.Empty;
        public int ResolveMinutes;
        public string SharedFileId = string.Empty;
        public string AttachmentName = string.Empty;
        public string AttachmentKind = string.Empty;
        public Dictionary<string, GroupActivity> Known = new Dictionary<string, GroupActivity>();
        public ConcurrentBag<string> ReplacedFiles = new ConcurrentBag<string>();
    }

    private sealed class PreparedPost
    {
        public string GroupId = string.Empty;
        public string AnnouncementId = string.Empty;
        public Task<List<Announcement>> OlderTask = Task.FromResult(new List<Announcement>());
        public string? Error;
    }

    private static Announcement BuildAnnouncement(string groupId, string announcementId, PostSettings s)
    {
        Announcement announcement = new Announcement();
        announcement.Id = announcementId;
        announcement.GroupId = groupId;
        announcement.BatchId = s.BatchId;
        announcement.Title = s.Title;
        announcement.Message = s.Message;
        announcement.IsActive = true;
        announcement.IsEdited = false;
        announcement.CreatedBy = s.Uid;
        announcement.Status = AnnouncementStatus.Active;
        announcement.ResolveMinutes = s.ResolveMinutes;
        announcement.SharedFileId = s.SharedFileId;
        announcement.AttachmentName = s.AttachmentName;
        announcement.AttachmentKind = s.AttachmentKind;
        return announcement;
    }

    // Posts to up to 12 groups at once: the announcement, the feed and the stats of every group go in ONE batch.
    // If the batch fails, each group is posted the old way, one by one, so each group still gets its own result.
    private async Task<List<GroupPostOutcome>> PostChunkAsync(List<string> groupIds, PostSettings s)
    {
        FirestoreBatch batch = _batches.Create();

        List<Task<PreparedPost>> preparing = new List<Task<PreparedPost>>();
        for (int i = 0; i < groupIds.Count; i++)
        {
            preparing.Add(PrepareAsync(batch, groupIds[i], s));
        }

        PreparedPost[] prepared = await Task.WhenAll(preparing);

        List<PreparedPost> ready = new List<PreparedPost>();
        for (int i = 0; i < prepared.Length; i++)
        {
            if (prepared[i].Error == null)
            {
                ready.Add(prepared[i]);
            }
        }

        bool committed = false;
        if (ready.Count > 0)
        {
            try
            {
                await ServiceHelper.WithTimeoutAsync(batch.CommitAsync());
                committed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PUBLISH BATCH ERROR (falling back to one group at a time): " + ex);
            }
        }

        List<GroupPostOutcome> outcomes = new List<GroupPostOutcome>();
        List<Task<GroupPostOutcome>> fallbacks = new List<Task<GroupPostOutcome>>();
        for (int i = 0; i < prepared.Length; i++)
        {
            GroupPostOutcome outcome = new GroupPostOutcome();
            outcome.GroupId = prepared[i].GroupId;
            if (prepared[i].Error != null)
            {
                outcome.Error = prepared[i].Error;
                outcomes.Add(outcome);
            }
            else if (committed)
            {
                outcome.Posted = true;
                outcomes.Add(outcome);
            }
            else
            {
                // Not posted by the batch: post this group alone (the result replaces this placeholder).
                outcomes.Add(outcome);
                fallbacks.Add(PostToGroupAsync(prepared[i].GroupId, s));
            }
        }

        if (fallbacks.Count > 0)
        {
            GroupPostOutcome[] single = await Task.WhenAll(fallbacks);
            for (int i = 0; i < single.Length; i++)
            {
                for (int j = 0; j < outcomes.Count; j++)
                {
                    if (outcomes[j].GroupId == single[i].GroupId)
                    {
                        outcomes[j] = single[i];
                    }
                }
            }
        }

        if (committed)
        {
            await CleanUpChunkAsync(ready, s);
        }

        return outcomes;
    }

    // Reads what the new announcement needs (the group's feed) and adds its three writes to the batch.
    private async Task<PreparedPost> PrepareAsync(FirestoreBatch batch, string groupId, PostSettings s)
    {
        PreparedPost post = new PreparedPost();
        post.GroupId = groupId;
        post.AnnouncementId = Guid.NewGuid().ToString("N");

        try
        {
            Announcement announcement = BuildAnnouncement(groupId, post.AnnouncementId, s);

            // The group's current announcements are read while the feed is read (they are cleaned up after the post).
            post.OlderTask = ReadAnnouncementsQuietlyAsync(groupId);

            await _feed.QueuePostedAsync(batch, groupId, announcement);
            _announcementRepository.QueueCreate(batch, groupId, post.AnnouncementId, announcement);
            _stats.QueuePosted(batch, groupId);
        }
        catch (TimeoutException)
        {
            post.Error = "Some requests took too long and may still go through. Refresh the DTC's announcements to check.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PUBLISH PREPARE ERROR: " + ex);
            post.Error = ServiceHelper.MapError(ex, "We could not post the announcement.");
        }

        return post;
    }

    // After a batch was saved: for every group clear the notice reports and the old announcement's reports / file,
    // then delete the old announcements of all the groups in one more batch. Best effort, never throws.
    private async Task CleanUpChunkAsync(List<PreparedPost> posted, PostSettings s)
    {
        try
        {
            List<Task<List<Announcement>>> jobs = new List<Task<List<Announcement>>>();
            for (int i = 0; i < posted.Count; i++)
            {
                jobs.Add(CleanUpGroupAsync(posted[i], s));
            }

            List<Announcement>[] olderLists = await Task.WhenAll(jobs);

            FirestoreBatch deletions = _batches.Create();
            List<Announcement> all = new List<Announcement>();
            for (int i = 0; i < olderLists.Length; i++)
            {
                for (int j = 0; j < olderLists[i].Count; j++)
                {
                    all.Add(olderLists[i][j]);
                    _announcementRepository.QueueDelete(deletions, olderLists[i][j].GroupId, olderLists[i][j].Id);
                }
            }

            if (all.Count == 0)
            {
                return;
            }

            try
            {
                await ServiceHelper.WithTimeoutAsync(deletions.CommitAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DELETE OLDER BATCH ERROR (deleting one by one): " + ex);
                await DeleteOneByOneAsync(all);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CLEAN UP CHUNK ERROR: " + ex);
        }
    }

    private async Task<List<Announcement>> CleanUpGroupAsync(PreparedPost post, PostSettings s)
    {
        Task noticeTask = ClearNoticePanicsAsync(post.GroupId, s);
        List<Announcement> older = await CollectOlderAsync(post.GroupId, post.AnnouncementId, post.OlderTask, s);
        await noticeTask;
        return older;
    }

    private async Task DeleteOneByOneAsync(List<Announcement> older)
    {
        List<Task> tasks = new List<Task>();
        for (int i = 0; i < older.Count; i++)
        {
            tasks.Add(DeleteQuietlyAsync(older[i].GroupId, older[i].Id));
        }

        await Task.WhenAll(tasks);
    }

    private async Task DeleteQuietlyAsync(string groupId, string announcementId)
    {
        try
        {
            await ServiceHelper.WithTimeoutAsync(_announcementRepository.DeleteAsync(groupId, announcementId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE OLDER ANNOUNCEMENT ERROR: " + ex);
        }
    }

    // Posts to ONE group the old way (announcement, then feed + stats). Used when a batch could not be saved.
    // Never throws: the outcome says whether the announcement was posted.
    private async Task<GroupPostOutcome> PostToGroupAsync(string groupId, PostSettings s)
    {
        GroupPostOutcome outcome = new GroupPostOutcome();
        outcome.GroupId = groupId;

        string announcementId = Guid.NewGuid().ToString("N");
        Announcement announcement = BuildAnnouncement(groupId, announcementId, s);

        try
        {
            // The group's current announcements are read while the new one is written (one round trip saved).
            Task<List<Announcement>> olderTask = ReadAnnouncementsQuietlyAsync(groupId);

            await ServiceHelper.WithTimeoutAsync(_announcementRepository.CreateAsync(groupId, announcementId, announcement));

            // The group's feed is what members listen to: if it cannot be written the post counts as failed for
            // this group (it stays selected). The stats are written at the same time and never fail a post.
            Task statsTask = _stats.PostedAsync(groupId);
            await _feed.PostedAsync(groupId, announcement);
            await statsTask;
            outcome.Posted = true;

            Task noticeTask = ClearNoticePanicsAsync(groupId, s);
            List<Announcement> older = await CollectOlderAsync(groupId, announcementId, olderTask, s);
            await DeleteOneByOneAsync(older);
            await noticeTask;
        }
        catch (TimeoutException)
        {
            outcome.Error = "Some requests took too long and may still go through. Refresh the DTC's announcements to check.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PUBLISH ANNOUNCEMENT ERROR: " + ex);
            outcome.Error = ServiceHelper.MapError(ex, "We could not post the announcement.");
        }

        return outcome;
    }

    // A new message replaces the 5-minute notice: its panic presses are cleared. When the live stats say there are
    // none (0 reports), the clean-up is skipped (it would only read an empty list).
    private async Task ClearNoticePanicsAsync(string groupId, PostSettings s)
    {
        GroupActivity? known;
        if (s.Known.TryGetValue(groupId, out known) && known.NoticePanicCount == 0)
        {
            return;
        }

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
    // This does the side work (panic reports, the file) and returns the older announcements that still have to be
    // deleted. Best effort: on a problem it returns what it has (the list still shows only the latest one).
    private async Task<List<Announcement>> CollectOlderAsync(string groupId, string keepId, Task<List<Announcement>> olderTask, PostSettings s)
    {
        List<Announcement> toDelete = new List<Announcement>();
        try
        {
            List<Announcement> all = await olderTask;

            // The newest older announcement is the one the stats counters belong to.
            Announcement? newestOlder = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == keepId)
                {
                    continue;
                }

                if (newestOlder == null || all[i].CreatedAt == default || (newestOlder.CreatedAt != default && all[i].CreatedAt > newestOlder.CreatedAt))
                {
                    newestOlder = all[i];
                }
            }

            GroupActivity? known;
            bool newestHasNoReports = s.Known.TryGetValue(groupId, out known) && known.HasAnnouncement && known.AnnouncementPanicCount == 0;

            List<Task> work = new List<Task>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == keepId)
                {
                    continue;
                }

                toDelete.Add(all[i]);
                bool skipReports = newestHasNoReports && ReferenceEquals(all[i], newestOlder);
                work.Add(CleanUpOlderAsync(groupId, all[i], skipReports, s));
            }

            await Task.WhenAll(work);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DELETE OLDER ANNOUNCEMENTS ERROR: " + ex);
        }

        return toDelete;
    }

    // The panic reports and the file of one older announcement. A shared file is only noted here: it is deleted
    // later, when no announcement uses it any more (SweepSharedFilesAsync).
    private async Task CleanUpOlderAsync(string groupId, Announcement older, bool skipReports, PostSettings s)
    {
        Task panicTask = skipReports ? Task.CompletedTask : _panics.DeleteAllAsync(groupId, older.Id);

        Task fileTask = Task.CompletedTask;
        if (!string.IsNullOrEmpty(older.SharedFileId))
        {
            s.ReplacedFiles.Add(older.SharedFileId);
        }
        else if (!string.IsNullOrEmpty(older.AttachmentKind))
        {
            fileTask = _attachments.DeleteAsync(groupId, older.Id);
        }

        await Task.WhenAll(panicTask, fileTask);
    }

    // One broadcast file is used by many groups. After the old announcements are gone, a file that no
    // announcement points at any more is deleted (1 read per file, 1 delete when unused). Best effort.
    private async Task SweepSharedFilesAsync(ConcurrentBag<string> replaced)
    {
        HashSet<string> done = new HashSet<string>();
        foreach (string fileId in replaced)
        {
            if (!done.Add(fileId))
            {
                continue;
            }

            try
            {
                bool stillUsed = await ServiceHelper.WithTimeoutAsync(_announcementRepository.ExistsWithSharedFileAsync(fileId));
                if (!stillUsed)
                {
                    await _attachments.DeleteSharedAsync(fileId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SWEEP SHARED FILE ERROR: " + ex);
            }
        }
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
