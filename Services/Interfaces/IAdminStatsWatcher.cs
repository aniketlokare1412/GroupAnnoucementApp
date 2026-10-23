using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

// Phase 3, admin side: ONE listener on the groupStats collection replaces the polling of admin Home.
// It costs one read per group when it starts and one per change afterwards (a post, a completion, a panic press).
public interface IAdminStatsWatcher
{
    // Raised (on any thread) when any group's stats changed.
    event Action? Changed;

    // True while the listener runs and has delivered its first answer.
    bool IsLive { get; }

    // Starts the listener (once) and waits for the first answer. Returns false when it cannot be used
    // (for example the new rules are not deployed yet): the caller then reads the old way.
    Task<bool> EnsureStartedAsync();

    // The stats of every group that has a stats document, as the admin screens need them. A copy.
    Dictionary<string, GroupActivity> Snapshot();

    // Stops the listener and forgets everything (sign out).
    void Stop();
}
