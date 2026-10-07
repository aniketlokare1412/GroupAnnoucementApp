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

    // Validates, blocks duplicate names (case-insensitive) and enforces GroupLimits.MaxActiveGroups.
    Task<OperationResult> CreateGroupAsync(string name, string description);

    Task<OperationResult> UpdateGroupAsync(string id, string name, string description);

    // Activating a group also checks GroupLimits.MaxActiveGroups.
    Task<OperationResult> SetActiveAsync(string id, bool isActive);
}
