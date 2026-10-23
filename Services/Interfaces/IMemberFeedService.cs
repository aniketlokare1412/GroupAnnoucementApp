using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.Services.Interfaces;

public enum FeedState
{
    // The first answer for this group has not arrived yet.
    Unknown,

    // The feed document exists.
    Ready,

    // The group has no feed document (not built yet): the caller reads the old collections instead.
    Missing,

    // The listener failed (for example the rules are not deployed yet): the caller reads the old collections instead.
    Failed
}

// Phase 2, member side: live listeners that replace the 20-second polling of Home.
//   - one listener on userGroups/{uid} (which groups the member is in)
//   - one listener on groupFeed/{groupId} for each of those groups (the latest message and outage)
// A listener costs one read when it starts and one per change; nothing while nothing changes.
public interface IMemberFeedService
{
    // Raised (on any thread) when a feed document arrived or changed.
    event Action? FeedChanged;

    // Raised (on any thread) when the member's group list changed on the server, for example an admin added the member.
    event Action? GroupsChanged;

    // Starts listening to the member's group list and waits for the first answer.
    // Throws when that is not possible (for example before the new rules are deployed): the caller then falls back to polling.
    Task<List<string>> StartAsync(string uid);

    // Listens to exactly these groups (starts new listeners, stops the others).
    void WatchGroups(IReadOnlyCollection<string> groupIds);

    // Completes when every watched group has answered once (or after the timeout).
    Task WaitForFirstAnswersAsync(TimeSpan timeout);

    FeedState GetState(string groupId, out GroupFeedDocument? feed);

    // Stops every listener and forgets everything (sign out).
    void Stop();
}
