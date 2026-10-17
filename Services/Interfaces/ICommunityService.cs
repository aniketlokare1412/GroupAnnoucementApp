using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Read-only view of the community structure for ANY signed-in user (members and admins).
// Members use it for the community headings, admins for the composer shortcut and the Home counts.
public interface ICommunityService
{
    // Communities and super communities, each sorted by name.
    Task<OperationResult<CommunityDirectory>> GetDirectoryAsync();
}
