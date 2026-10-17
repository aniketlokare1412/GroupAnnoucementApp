using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface ICommunityRepository
{
    // All methods throw on failure. The service layer catches and converts to OperationResult.

    Task CreateAsync(string id, Community community);

    // Returns null when the document does not exist.
    Task<Community?> GetByIdAsync(string id);

    // Any active user may read communities (members see them as headings).
    Task<List<Community>> GetAllAsync();

    // Changes every editable field in ONE write, so the links can never be half-saved.
    Task UpdateAsync(string id, string name, string description, string number, List<string> groupIds, string superCommunityId);

    // Used when a super community's list of communities changes. Empty string = no super community.
    Task SetSuperCommunityAsync(string id, string superCommunityId);
}
