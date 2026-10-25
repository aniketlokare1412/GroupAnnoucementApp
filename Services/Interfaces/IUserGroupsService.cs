namespace GroupAnnouncementApp.Services.Interfaces;

// Phase 2: the ids of the groups a user is in, kept in userGroups/{uid}.
// These methods throw on failure; the callers (membership services) already catch and convert.
public interface IUserGroupsService
{
    // The user's group ids. Answered from memory when known, otherwise from userGroups/{uid};
    // a user who has no such document yet is repaired from the members collection group first.
    Task<List<string>> GetIdsAsync(string uid);

    // Call AFTER groups/{groupId}/members/{uid} was created.
    Task AddAsync(string uid, string groupId);

    // Call AFTER groups/{groupId}/members/{uid} was deleted.
    Task RemoveAsync(string uid, string groupId);

    // Rebuilds the list from the members collection group (the truth) and stores it. Costs one read per membership.
    Task<List<string>> ReconcileAsync(string uid);

    // Used by the live listener to keep the in-memory copy fresh.
    void SetKnown(string uid, List<string> groupIds);

    void Clear();
}
