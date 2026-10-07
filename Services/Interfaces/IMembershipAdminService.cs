using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Admin-only member management. The checks here are a convenience for friendly messages;
// the real protection is the Firestore security rules.
public interface IMembershipAdminService
{
    // The members of a group, sorted by name.
    Task<OperationResult<List<GroupMemberEntry>>> GetMembersAsync(string groupId);

    // Active Regular users who are not yet members of the group, sorted by name.
    Task<OperationResult<List<UserProfile>>> GetEligibleUsersAsync(string groupId);

    // Adds the users one by one. Returns how many were added.
    // If one fails, the users added before it stay added and the message says how many.
    Task<OperationResult<int>> AddMembersAsync(string groupId, List<string> userIds);

    Task<OperationResult> RemoveMemberAsync(string groupId, string userId);
}
