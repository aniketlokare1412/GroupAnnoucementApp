using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Admin-only community and super community operations. The checks here give friendly messages;
// the real protection is the Firestore rules. Communities cannot be deleted or deactivated.
public interface ICommunityAdminService
{
    // Sorted by name.
    Task<OperationResult<List<Community>>> GetAllCommunitiesAsync();

    Task<OperationResult<Community?>> GetCommunityAsync(string id);

    Task<OperationResult<List<SuperCommunity>>> GetAllSuperCommunitiesAsync();

    Task<OperationResult<SuperCommunity?>> GetSuperCommunityAsync(string id);

    // groupIds = the groups in the community. superCommunityId = "" for none.
    Task<OperationResult> CreateCommunityAsync(string name, string description, string number, List<string> groupIds, string superCommunityId);

    Task<OperationResult> UpdateCommunityAsync(string id, string name, string description, string number, List<string> groupIds, string superCommunityId);

    // communityIds = the communities that should sit under this super community. A community
    // that was under another super community is moved (it can only have one).
    Task<OperationResult> CreateSuperCommunityAsync(string name, string description, string number, List<string> communityIds);

    Task<OperationResult> UpdateSuperCommunityAsync(string id, string name, string description, string number, List<string> communityIds);
}
