using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IAdminProfileRepository
{
    // Both methods throw on failure. The service layer catches and converts.
    Task SetAsync(string uid, AdminProfile profile);

    // Returns null when the admin has no details saved (an admin from before the request form existed).
    Task<AdminProfile?> GetAsync(string uid);
}
