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

    public MembershipAdminService(
        ISessionService session,
        IAuthService authService,
        IUserRepository userRepository,
        IMembershipRepository membershipRepository)
    {
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
            return OperationResult<List<GroupMemberEntry>>.Fail("Unknown group.");
        }

        try
        {
            List<GroupMember> members = await ServiceHelper.WithTimeoutAsync(_membershipRepository.GetByGroupAsync(groupId));
            List<UserProfile> users = await ServiceHelper.WithTimeoutAsync(_userRepository.GetAllAsync());

            Dictionary<string, UserProfile> usersById = new Dictionary<string, UserProfile>();
            for (int i = 0; i < users.Count; i++)
            {
                usersById[users[i].Id] = users[i];
            }

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
            return OperationResult<List<UserProfile>>.Fail("Unknown group.");
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

    // Active Regular users who are not yet in the group. Throws on failure.
    private async Task<List<UserProfile>> LoadEligibleUsersAsync(string groupId)
    {
        List<GroupMember> members = await ServiceHelper.WithTimeoutAsync(_membershipRepository.GetByGroupAsync(groupId));
        List<UserProfile> users = await ServiceHelper.WithTimeoutAsync(_userRepository.GetAllAsync());

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
            return "Unknown group.";
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
