using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class MemberFeedService : IMemberFeedService
{
    private sealed class FeedEntry
    {
        public IDisposable? Handle;
        public bool Loaded;
        public bool Failed;
        public GroupFeedDocument? Document;
    }

    private readonly IGroupFeedRepository _feeds;
    private readonly IUserGroupsRepository _userGroupsRepository;
    private readonly IUserGroupsService _userGroups;

    private readonly object _gate = new object();
    private readonly Dictionary<string, FeedEntry> _entries = new Dictionary<string, FeedEntry>();

    private string? _uid;
    private IDisposable? _userHandle;
    private List<string>? _ids;
    private TaskCompletionSource<bool>? _firstIds;
    private bool _repairing;
    private TaskCompletionSource<bool>? _firstAnswers;

    public event Action? FeedChanged;

    public event Action? GroupsChanged;

    public MemberFeedService(IGroupFeedRepository feeds, IUserGroupsRepository userGroupsRepository, IUserGroupsService userGroups)
    {
        _feeds = feeds;
        _userGroupsRepository = userGroupsRepository;
        _userGroups = userGroups;
    }

    public async Task<List<string>> StartAsync(string uid)
    {
        TaskCompletionSource<bool> first;
        lock (_gate)
        {
            if (_uid == uid && _userHandle != null && _ids != null)
            {
                return new List<string>(_ids);
            }

            if (_uid == uid && _userHandle != null && _firstIds != null)
            {
                first = _firstIds;
            }
            else
            {
                StopLocked();
                _uid = uid;
                first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _firstIds = first;
                _userHandle = _userGroupsRepository.Watch(
                    uid,
                    ids => OnUserIds(uid, ids),
                    error => OnUserError(uid, error));
            }
        }

        Task finished = await Task.WhenAny(first.Task, Task.Delay(TimeSpan.FromSeconds(ServiceHelper.TimeoutSeconds)));
        if (finished != first.Task)
        {
            throw new TimeoutException();
        }

        await first.Task;

        lock (_gate)
        {
            return _ids == null ? new List<string>() : new List<string>(_ids);
        }
    }

    public void WatchGroups(IReadOnlyCollection<string> groupIds)
    {
        lock (_gate)
        {
            HashSet<string> wanted = new HashSet<string>(groupIds);

            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, FeedEntry> pair in _entries)
            {
                if (!wanted.Contains(pair.Key))
                {
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _entries[remove[i]].Handle?.Dispose();
                _entries.Remove(remove[i]);
            }

            _firstAnswers = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            foreach (string groupId in wanted)
            {
                if (_entries.ContainsKey(groupId))
                {
                    continue;
                }

                FeedEntry entry = new FeedEntry();
                _entries[groupId] = entry;
                string id = groupId;
                entry.Handle = _feeds.Watch(
                    id,
                    document => OnFeed(id, document),
                    error => OnFeedError(id, error));
            }

            CompleteFirstAnswersIfReadyLocked();
        }
    }

    public async Task WaitForFirstAnswersAsync(TimeSpan timeout)
    {
        Task? wait;
        lock (_gate)
        {
            wait = _firstAnswers?.Task;
        }

        if (wait == null || wait.IsCompleted)
        {
            return;
        }

        await Task.WhenAny(wait, Task.Delay(timeout));
    }

    public FeedState GetState(string groupId, out GroupFeedDocument? feed)
    {
        lock (_gate)
        {
            feed = null;
            FeedEntry? entry;
            if (!_entries.TryGetValue(groupId, out entry) || !entry.Loaded)
            {
                return FeedState.Unknown;
            }

            if (entry.Failed)
            {
                return FeedState.Failed;
            }

            if (entry.Document == null)
            {
                return FeedState.Missing;
            }

            feed = entry.Document;
            return FeedState.Ready;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }

        _userGroups.Clear();
    }

    private void StopLocked()
    {
        foreach (KeyValuePair<string, FeedEntry> pair in _entries)
        {
            pair.Value.Handle?.Dispose();
        }

        _entries.Clear();
        _userHandle?.Dispose();
        _userHandle = null;
        _uid = null;
        _ids = null;
        _firstIds = null;
        _firstAnswers = null;
        _repairing = false;
    }

    private void OnFeed(string groupId, GroupFeedDocument? document)
    {
        lock (_gate)
        {
            FeedEntry? entry;
            if (!_entries.TryGetValue(groupId, out entry))
            {
                return;
            }

            entry.Loaded = true;
            entry.Failed = false;
            entry.Document = document;
            CompleteFirstAnswersIfReadyLocked();
        }

        FeedChanged?.Invoke();
    }

    private void OnFeedError(string groupId, Exception error)
    {
        System.Diagnostics.Debug.WriteLine("FEED LISTENER ERROR (" + groupId + "): " + error.Message);
        lock (_gate)
        {
            FeedEntry? entry;
            if (!_entries.TryGetValue(groupId, out entry))
            {
                return;
            }

            entry.Loaded = true;
            entry.Failed = true;
            entry.Document = null;
            CompleteFirstAnswersIfReadyLocked();
        }

        FeedChanged?.Invoke();
    }

    private void CompleteFirstAnswersIfReadyLocked()
    {
        if (_firstAnswers == null)
        {
            return;
        }

        foreach (KeyValuePair<string, FeedEntry> pair in _entries)
        {
            if (!pair.Value.Loaded)
            {
                return;
            }
        }

        _firstAnswers.TrySetResult(true);
    }

    private void OnUserIds(string uid, List<string>? ids)
    {
        if (ids == null)
        {
            // This member has no userGroups document yet: build it once from the members collection group.
            _ = RepairAsync(uid);
            return;
        }

        bool changed;
        TaskCompletionSource<bool>? first;
        lock (_gate)
        {
            if (_uid != uid)
            {
                return;
            }

            changed = _ids != null && !SameSet(_ids, ids);
            _ids = new List<string>(ids);
            _repairing = false;
            first = _firstIds;
        }

        _userGroups.SetKnown(uid, ids);
        first?.TrySetResult(true);

        if (changed)
        {
            GroupsChanged?.Invoke();
        }
    }

    private async Task RepairAsync(string uid)
    {
        lock (_gate)
        {
            if (_repairing || _uid != uid)
            {
                return;
            }

            _repairing = true;
        }

        try
        {
            List<string> ids = await _userGroups.ReconcileAsync(uid);
            OnUserIds(uid, ids);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("USER GROUPS REPAIR ERROR: " + ex);
            lock (_gate)
            {
                _repairing = false;
            }

            OnUserError(uid, ex);
        }
    }

    private void OnUserError(string uid, Exception error)
    {
        System.Diagnostics.Debug.WriteLine("USER GROUPS LISTENER ERROR: " + error.Message);
        TaskCompletionSource<bool>? first;
        lock (_gate)
        {
            if (_uid != uid)
            {
                return;
            }

            first = _firstIds;
        }

        first?.TrySetException(error);
    }

    private static bool SameSet(List<string> a, List<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        HashSet<string> set = new HashSet<string>(a);
        for (int i = 0; i < b.Count; i++)
        {
            if (!set.Contains(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
