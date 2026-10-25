using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Repositories.Interfaces;

public interface IDirectoryRepository
{
    // All methods throw on failure. The service layer catches.

    // directory/main. Returns null when the document does not exist yet (before the first rebuild).
    Task<DirectoryDocument?> GetMainAsync();

    // directory/admin (admins only). Returns null when it does not exist yet.
    Task<DirectoryDocument?> GetAdminAsync();

    // Replace the whole document. Admin only (enforced by the Firestore rules).
    Task SetMainAsync(string json);

    Task SetAdminAsync(string json);
}
