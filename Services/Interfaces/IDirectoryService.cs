using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Phase 1: reads the group / community / super community lists from the two small "directory"
// documents (1-2 reads) instead of one read per group. Every read method returns null when the
// directory cannot be used (not built yet, not readable, damaged): the caller then falls back to the
// old collection queries, so the app keeps working before the migration has been run.
public interface IDirectoryService
{
    // Active groups, communities and super communities. Served from memory when the last read is
    // younger than maxAgeSeconds (0 = always read).
    Task<DirectorySnapshot?> GetAsync(int maxAgeSeconds);

    // Admin only: the same plus the inactive groups. Builds the directory once per session when it is missing.
    Task<DirectorySnapshot?> GetForAdminAsync(int maxAgeSeconds);

    // Admin action "Rebuild directory": reads the source collections and rewrites both documents. Safe to repeat.
    Task<OperationResult<DirectoryRebuildInfo>> RebuildAsync();

    // Called by the admin services after a group / community / super community changed.
    // Never throws: when the rebuild fails, admin screens use the old queries until the next successful rebuild.
    Task TryRebuildAsync();
}
