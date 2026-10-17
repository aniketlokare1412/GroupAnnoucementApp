using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface ISuperCommunityRepository
{
    Task CreateAsync(string id, SuperCommunity superCommunity);

    // Returns null when the document does not exist.
    Task<SuperCommunity?> GetByIdAsync(string id);

    Task<List<SuperCommunity>> GetAllAsync();

    Task UpdateDetailsAsync(string id, string name, string description, string number);
}
