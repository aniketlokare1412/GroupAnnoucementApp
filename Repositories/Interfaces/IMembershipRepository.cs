using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IMembershipRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    // Creates groups/{groupId}/members/{userId}. addedBy is the uid of whoever is adding.
    Task AddAsync(string groupId, string userId, string addedBy);

    // Deletes the member document. Does nothing if it does not exist.
    Task RemoveAsync(string groupId, string userId);

    // Returns null when the user is not a member of the group.
    Task<GroupMember?> GetAsync(string groupId, string userId);

    Task<List<GroupMember>> GetByGroupAsync(string groupId);

    // Collection group query: the ids of every group the user is a member of.
    Task<List<string>> GetGroupIdsForUserAsync(string userId);
}
