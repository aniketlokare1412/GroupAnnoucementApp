using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// What a Regular user can do about their own memberships.
// The checks here are a convenience for friendly messages;
// the real protection is the Firestore security rules.
public interface IMembershipService
{
    // The ACTIVE groups the signed-in user is a member of, sorted by name.
    Task<OperationResult<List<AnnouncementGroup>>> GetMyGroupsAsync();

    // Every ACTIVE group, each flagged with whether the user is already a member. Sorted by name.
    // If only the membership lookup fails, the groups are still returned (MembershipKnown = false).
    Task<OperationResult<BrowseGroupsResult>> GetBrowseGroupsAsync();

    Task<OperationResult> JoinGroupAsync(string groupId);

    Task<OperationResult> LeaveGroupAsync(string groupId);

    // Phase 2: re-checks the member's group list against the members collection and repairs it
    // (for example after a join whose second write failed). Costs one read per membership: used on pull-to-refresh only.
    Task<OperationResult> ReconcileGroupsAsync();
}
