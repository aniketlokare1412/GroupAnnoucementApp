using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Admin-only group operations. The checks here are a convenience for friendly messages;
// the real protection is the Firestore security rules.
public interface IGroupAdminService
{
    // All groups (active and inactive), sorted by name.
    Task<OperationResult<List<AnnouncementGroup>>> GetAllGroupsAsync();

    Task<OperationResult<AnnouncementGroup?>> GetGroupAsync(string id);

    // Validates and blocks duplicate names and numbers (case-insensitive). There is no limit on the number of groups.
    Task<OperationResult> CreateGroupAsync(string name, string description, string number);

    // Same as above, and also adds the new group to the chosen communities (AB Switches). The list may be empty.
    // On success the value is null, or a short warning when the group was created but some communities could not be updated.
    Task<OperationResult<string?>> CreateGroupAsync(string name, string description, string number, List<string> communityIds);

    Task<OperationResult> UpdateGroupAsync(string id, string name, string description, string number);

    Task<OperationResult> SetActiveAsync(string id, bool isActive);
}
