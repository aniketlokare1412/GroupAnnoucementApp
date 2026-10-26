using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class MembershipAdminService : IMembershipAdminService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;
    private readonly IMembershipRepository _membershipRepository;
    private readonly IUserGroupsService _userGroups;
    private readonly IUserListCache _userCache;

    public MembershipAdminService(
        ISessionService session,
        IAuthService authService,
        IUserRepository userRepository,
        IMembershipRepository membershipRepository,
        IUserGroupsService userGroups,
        IUserListCache userCache)
    {
        _userCache = userCache;
        _userGroups = userGroups;
        _session = session;
        _authService = authService;
        _userRepository = userRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<OperationResult<List<GroupMemberEntry>>> GetMembersAsync(string groupId)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<GroupMemberEntry>>.Fail("You do not have permission to view members.");
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return OperationResult<List<GroupMemberEntry>>.Fail("Unknown DTC.");
        }

        try
        {
            List<GroupMember> members = await ServiceHelper.WithTimeoutAsync(_membershipRepository.GetByGroupAsync(groupId));
            // Phase 5: only the members' own profiles are read (a group has a handful of members), not every user.
            Dictionary<string, UserProfile> usersById = await LoadMemberProfilesAsync(members);

            List<GroupMemberEntry> entries = new List<GroupMemberEntry>();
            for (int i = 0; i < members.Count; i++)
            {
                entries.Add(BuildEntry(members[i].Id, usersById));
            }

            entries.Sort(CompareByName);
            return OperationResult<List<GroupMemberEntry>>.Success(entries);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<GroupMemberEntry>>.Fail("Loading members is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD MEMBERS ERROR: " + ex);
            return OperationResult<List<GroupMemberEntry>>.Fail(ServiceHelper.MapError(ex, "We could not load the members. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<List<UserProfile>>> GetEligibleUsersAsync(string groupId)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<UserProfile>>.Fail("You do not have permission to add members.");
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return OperationResult<List<UserProfile>>.Fail("Unknown DTC.");
        }

        try
        {
            List<UserProfile> eligible = await LoadEligibleUsersAsync(groupId);
            return OperationResult<List<UserProfile>>.Success(eligible);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<UserProfile>>.Fail("Loading users is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD ELIGIBLE USERS ERROR: " + ex);
            return OperationResult<List<UserProfile>>.Fail(ServiceHelper.MapError(ex, "We could not load the users. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<int>> AddMembersAsync(string groupId, List<string> userIds)
    {
        string? problem = CheckCanModify(groupId);
        if (problem == null && (userIds == null || userIds.Count == 0))
        {
            problem = "Select at least one user.";
        }
        if (problem != null)
        {
            return OperationResult<int>.Fail(problem);
        }

        string? adminUid = _authService.GetCurrentUserId();
        if (string.IsNullOrEmpty(adminUid))
        {
            return OperationResult<int>.Fail("You are signed out. Please sign in again.");
        }

        int added = 0;
        try
        {
            // Re-check against fresh data: someone may have joined, or been deactivated, meanwhile.
            List<UserProfile> eligible = await LoadEligibleUsersAsync(groupId);
            HashSet<string> eligibleIds = new HashSet<string>();
            for (int i = 0; i < eligible.Count; i++)
            {
                eligibleIds.Add(eligible[i].Id);
            }

            List<string> toAdd = new List<string>();
            for (int i = 0; i < userIds!.Count; i++)
            {
                if (eligibleIds.Contains(userIds[i]) && !toAdd.Contains(userIds[i]))
                {
                    toAdd.Add(userIds[i]);
                }
            }

            if (toAdd.Count == 0)
            {
                return OperationResult<int>.Fail("The selected users can no longer be added. Please refresh the list.");
            }

            for (int i = 0; i < toAdd.Count; i++)
            {
                await ServiceHelper.WithTimeoutAsync(_membershipRepository.AddAsync(groupId, toAdd[i], adminUid));

                // Phase 2: the member's own list of group ids (what the member's Home listens to).
                await _userGroups.AddAsync(toAdd[i], groupId);
                added++;
            }

            return OperationResult<int>.Success(added);
        }
        catch (TimeoutException)
        {
            return OperationResult<int>.Fail(Prefix(added, userIds!.Count) + "The request is taking too long. Check the members list for the current status.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ADD MEMBERS ERROR: " + ex);
            return OperationResult<int>.Fail(Prefix(added, userIds!.Count) + ServiceHelper.MapError(ex, "We could not add the members. Please try again."));
        }
    }

    public async Task<OperationResult> RemoveMemberAsync(string groupId, string userId)
    {
        string? problem = CheckCanModify(groupId);
        if (problem == null && string.IsNullOrEmpty(userId))
        {
            problem = "Unknown user.";
        }
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        try
        {
            await ServiceHelper.WithTimeoutAsync(_membershipRepository.RemoveAsync(groupId, userId));
            await _userGroups.RemoveAsync(userId, groupId);
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see the current members.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REMOVE MEMBER ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not remove the member. Please try again."));
        }
    }

    // The profiles of the given members: from the user cache when it is fresh (no reads), otherwise one read each
    // (10 at a time). A member whose profile no longer exists is left out (the entry then shows as unknown, as before).
    private async Task<Dictionary<string, UserProfile>> LoadMemberProfilesAsync(List<GroupMember> members)
    {
        Dictionary<string, UserProfile> result = new Dictionary<string, UserProfile>();
        List<string> missing = new List<string>();
        for (int i = 0; i < members.Count; i++)
        {
            UserProfile? cached = _userCache.TryGetFresh(members[i].Id);
            if (cached != null)
            {
                result[members[i].Id] = cached;
            }
            else
            {
                missing.Add(members[i].Id);
            }
        }

        using (SemaphoreSlim gate = new SemaphoreSlim(10))
        {
            List<Task<UserProfile?>> reads = new List<Task<UserProfile?>>();
            for (int i = 0; i < missing.Count; i++)
            {
                string uid = missing[i];
                reads.Add(ReadProfileLimitedAsync(gate, uid));
            }

            await Task.WhenAll(reads);
            for (int i = 0; i < reads.Count; i++)
            {
                UserProfile? profile = reads[i].Result;
                if (profile != null)
                {
                    result[profile.Id] = profile;
                }
            }
        }

        return result;
    }

    private async Task<UserProfile?> ReadProfileLimitedAsync(SemaphoreSlim gate, string uid)
    {
        await gate.WaitAsync();
        try
        {
            return await ServiceHelper.WithTimeoutAsync(_userRepository.GetByIdAsync(uid));
        }
        finally
        {
            gate.Release();
        }
    }

    // Active Regular users who are not yet in the group. Throws on failure.
    private async Task<List<UserProfile>> LoadEligibleUsersAsync(string groupId)
    {
        List<GroupMember> members = await ServiceHelper.WithTimeoutAsync(_membershipRepository.GetByGroupAsync(groupId));

        // Phase 5: the full user list is kept for 30 minutes (see UserListCache), not read on every visit.
        List<UserProfile> users = await ServiceHelper.WithTimeoutAsync(_userCache.GetAllAsync(false));

        HashSet<string> memberIds = new HashSet<string>();
        for (int i = 0; i < members.Count; i++)
        {
            memberIds.Add(members[i].Id);
        }

        List<UserProfile> eligible = new List<UserProfile>();
        for (int i = 0; i < users.Count; i++)
        {
            UserProfile user = users[i];
            bool isRegular = string.Equals(user.UserType, UserTypes.Regular, StringComparison.Ordinal);

            if (user.IsActive && isRegular && !memberIds.Contains(user.Id))
            {
                eligible.Add(user);
            }
        }

        eligible.Sort(CompareUsersByName);
        return eligible;
    }

    private static GroupMemberEntry BuildEntry(string userId, Dictionary<string, UserProfile> usersById)
    {
        GroupMemberEntry entry = new GroupMemberEntry();
        entry.UserId = userId;

        UserProfile? profile;
        if (usersById.TryGetValue(userId, out profile))
        {
            entry.Email = profile.Email;
            entry.IsUserActive = profile.IsActive;
            entry.IsAdmin = string.Equals(profile.UserType, UserTypes.Admin, StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                entry.Name = profile.Email;
            }
            else
            {
                entry.Name = profile.Name;
            }
        }
        else
        {
            entry.ProfileMissing = true;
            entry.Name = "Unknown user";
        }

        return entry;
    }

    // Returns null when the change is allowed, otherwise a friendly message.
    private string? CheckCanModify(string groupId)
    {
        if (!_session.IsAdmin)
        {
            return "You do not have permission to change members.";
        }

        if (string.IsNullOrEmpty(groupId))
        {
            return "Unknown DTC.";
        }

        // Without a connection Firestore would queue the write and apply it later,
        // which is confusing for an admin action. So we refuse up front.
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return "You are offline. Please connect to the internet and try again.";
        }

        return null;
    }

    private static string Prefix(int added, int total)
    {
        if (added == 0)
        {
            return string.Empty;
        }

        return "Added " + added + " of " + total + " members. ";
    }

    private static int CompareByName(GroupMemberEntry a, GroupMemberEntry b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareUsersByName(UserProfile a, UserProfile b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }
}
