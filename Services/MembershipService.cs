using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class MembershipService : IMembershipService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IGroupRepository _groupRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IDirectoryService _directory;
    private readonly IUserGroupsService _userGroups;

    // Home's refresh asks for "my groups" every 20 seconds. Group names and numbers rarely change,
    // so a directory copy up to 60 seconds old replaces one read per group. Groups that are not in the
    // copy (for example one created seconds ago) are still read one by one, exactly as before.
    private const int DirectoryMaxAgeSeconds = 60;

    public MembershipService(
        ISessionService session,
        IAuthService authService,
        IGroupRepository groupRepository,
        IMembershipRepository membershipRepository,
        IDirectoryService directory,
        IUserGroupsService userGroups)
    {
        _userGroups = userGroups;
        _session = session;
        _authService = authService;
        _groupRepository = groupRepository;
        _membershipRepository = membershipRepository;
        _directory = directory;
    }

    public async Task<OperationResult<List<AnnouncementGroup>>> GetMyGroupsAsync()
    {
        string? uid = GetMemberUid();
        if (uid == null)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("Only members can view their groups.");
        }

        try
        {
            HashSet<string> memberOf = await LoadMemberGroupIdsAsync(uid);

            // Only the member's own groups are read (a handful), not every active group in the app.
            List<Task<AnnouncementGroup?>> lookups = new List<Task<AnnouncementGroup?>>();
            DirectorySnapshot? snapshot = await _directory.GetAsync(DirectoryMaxAgeSeconds);
            foreach (string groupId in memberOf)
            {
                AnnouncementGroup? known = snapshot?.FindGroup(groupId);
                if (known != null)
                {
                    lookups.Add(Task.FromResult<AnnouncementGroup?>(DirectorySnapshot.CloneGroup(known)));
                }
                else
                {
                    lookups.Add(ReadVisibleGroupAsync(groupId));
                }
            }

            await Task.WhenAll(lookups);

            List<AnnouncementGroup> mine = new List<AnnouncementGroup>();
            for (int i = 0; i < lookups.Count; i++)
            {
                AnnouncementGroup? group = lookups[i].Result;
                if (group != null && group.IsActive)
                {
                    mine.Add(group);
                }
            }

            mine.Sort(CompareByName);
            return OperationResult<List<AnnouncementGroup>>.Success(mine);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("Loading your groups is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD MY GROUPS ERROR: " + ex);
            return OperationResult<List<AnnouncementGroup>>.Fail(ServiceHelper.MapError(ex, "We could not load your groups. Please check your connection and try again."));
        }
    }

    // One group by id. A group that members may not read (inactive, or gone) counts as "not there",
    // exactly like the old "all active groups" query that never returned it.
    private async Task<AnnouncementGroup?> ReadVisibleGroupAsync(string groupId)
    {
        try
        {
            return await ServiceHelper.WithTimeoutAsync(_groupRepository.GetByIdAsync(groupId));
        }
        catch (Exception ex) when (IsPermissionDenied(ex))
        {
            return null;
        }
    }

    private static bool IsPermissionDenied(Exception ex)
    {
        string text = ex.Message ?? string.Empty;
        return text.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
            || text.Contains("permission", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<OperationResult<BrowseGroupsResult>> GetBrowseGroupsAsync()
    {
        string? uid = GetMemberUid();
        if (uid == null)
        {
            return OperationResult<BrowseGroupsResult>.Fail("Only members can join groups.");
        }

        try
        {
            // The active groups are essential: without them there is nothing to search.
            // Phase 1: one directory read (always fresh here, so a group created a moment ago is listed)
            // instead of one read per active group. Falls back to the old query before the directory exists.
            DirectorySnapshot? snapshot = await _directory.GetAsync(0);
            List<AnnouncementGroup> active = snapshot != null
                ? snapshot.CopyGroups(true)
                : await ServiceHelper.WithTimeoutAsync(_groupRepository.GetActiveAsync());
            active.Sort(CompareByName);

            // Showing which groups the user already joined is nice to have, but searching and
            // joining must keep working if this lookup fails (for example while the collection
            // group index is still being built). Joining re-checks membership anyway.
            HashSet<string> memberOf = new HashSet<string>();
            bool membershipKnown = true;
            try
            {
                memberOf = await LoadMemberGroupIdsAsync(uid);
            }
            catch (Exception lookupError)
            {
                System.Diagnostics.Debug.WriteLine("LOAD MEMBERSHIPS ERROR: " + lookupError);
                membershipKnown = false;
            }

            BrowseGroupsResult browse = new BrowseGroupsResult();
            browse.MembershipKnown = membershipKnown;
            for (int i = 0; i < active.Count; i++)
            {
                JoinableGroup item = new JoinableGroup();
                item.Group = active[i];
                item.IsMember = memberOf.Contains(active[i].Id);
                browse.Groups.Add(item);
            }

            return OperationResult<BrowseGroupsResult>.Success(browse);
        }
        catch (TimeoutException)
        {
            return OperationResult<BrowseGroupsResult>.Fail("Loading groups is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD BROWSE GROUPS ERROR: " + ex);
            return OperationResult<BrowseGroupsResult>.Fail(ServiceHelper.MapError(ex, "We could not load the groups. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult> JoinGroupAsync(string groupId)
    {
        string? uid = GetMemberUid();
        if (uid == null)
        {
            return OperationResult.Fail("Only members can join groups.");
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return OperationResult.Fail("Unknown group.");
        }

        if (!IsOnline())
        {
            return OperationResult.Fail("You are offline. Please connect to the internet and try again.");
        }

        try
        {
            DirectorySnapshot? snapshot = await _directory.GetAsync(0);
            List<AnnouncementGroup> active = snapshot != null
                ? snapshot.CopyGroups(true)
                : await ServiceHelper.WithTimeoutAsync(_groupRepository.GetActiveAsync());
            if (!ContainsGroup(active, groupId))
            {
                return OperationResult.Fail("This group is no longer available.");
            }

            GroupMember? existing = await ServiceHelper.WithTimeoutAsync(_membershipRepository.GetAsync(groupId, uid));
            if (existing != null)
            {
                return OperationResult.Fail("You are already a member of this group.");
            }

            await ServiceHelper.WithTimeoutAsync(_membershipRepository.AddAsync(groupId, uid, uid));
            await UpdateGroupIdsAsync(() => _userGroups.AddAsync(uid, groupId), uid, groupId, true);
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see whether you joined.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("JOIN GROUP ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not join the group. Please try again."));
        }
    }

    public async Task<OperationResult> LeaveGroupAsync(string groupId)
    {
        string? uid = GetMemberUid();
        if (uid == null)
        {
            return OperationResult.Fail("Only members can leave groups.");
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return OperationResult.Fail("Unknown group.");
        }

        if (!IsOnline())
        {
            return OperationResult.Fail("You are offline. Please connect to the internet and try again.");
        }

        try
        {
            await ServiceHelper.WithTimeoutAsync(_membershipRepository.RemoveAsync(groupId, uid));
            await UpdateGroupIdsAsync(() => _userGroups.RemoveAsync(uid, groupId), uid, groupId, false);
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see the current status.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LEAVE GROUP ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not leave the group. Please try again."));
        }
    }

    // Phase 2: keeps userGroups/{uid} in step. The membership itself is already saved, so a failure here must
    // not turn a successful join / leave into an error: the list is retried once, then repaired on the next pull-to-refresh.
    private async Task UpdateGroupIdsAsync(Func<Task> update, string uid, string groupId, bool joined)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await update();
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USER GROUPS UPDATE ERROR: " + ex);
            }
        }

        // This session still sees the change.
        try
        {
            List<string> ids = await _userGroups.GetIdsAsync(uid);
            if (joined && !ids.Contains(groupId))
            {
                ids.Add(groupId);
            }
            else if (!joined)
            {
                ids.Remove(groupId);
            }

            _userGroups.SetKnown(uid, ids);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("USER GROUPS LOCAL UPDATE ERROR: " + ex);
        }
    }

    public async Task<OperationResult> ReconcileGroupsAsync()
    {
        string? uid = GetMemberUid();
        if (uid == null)
        {
            return OperationResult.Fail("Only members can view their groups.");
        }

        try
        {
            await _userGroups.ReconcileAsync(uid);
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("Loading your groups is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("RECONCILE GROUPS ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not refresh your groups. Please check your connection and try again."));
        }
    }

    // The uid of the signed-in Regular user, or null for signed-out users and admins.
    // Admins see every group in Manage groups, so they never join groups themselves.
    private string? GetMemberUid()
    {
        if (_session.CurrentProfile == null || _session.IsAdmin)
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

    private async Task<HashSet<string>> LoadMemberGroupIdsAsync(string uid)
    {
        // Phase 2: from the member's own userGroups document (memory copy when the live listener is running)
        // instead of a collection group query over every member document.
        List<string> ids = await _userGroups.GetIdsAsync(uid);

        HashSet<string> set = new HashSet<string>();
        for (int i = 0; i < ids.Count; i++)
        {
            set.Add(ids[i]);
        }

        return set;
    }

    private static bool ContainsGroup(List<AnnouncementGroup> groups, string groupId)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Id == groupId)
            {
                return true;
            }
        }

        return false;
    }

    // Without a connection Firestore would queue the write and apply it later,
    // which is confusing for a join or leave action. So we refuse up front.
    private static bool IsOnline()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private static int CompareByName(AnnouncementGroup a, AnnouncementGroup b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }
}
