using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class AdminStatsWatcher : IAdminStatsWatcher
{
    private readonly IGroupStatsRepository _stats;
    private readonly object _gate = new object();

    private IDisposable? _handle;
    private TaskCompletionSource<bool>? _first;
    private Dictionary<string, GroupStatsDocument> _docs = new Dictionary<string, GroupStatsDocument>();
    private bool _live;
    private bool _failed;

    public event Action? Changed;

    public AdminStatsWatcher(IGroupStatsRepository stats)
    {
        _stats = stats;
    }

    public bool IsLive
    {
        get { lock (_gate) { return _live; } }
    }

    public async Task<bool> EnsureStartedAsync()
    {
        TaskCompletionSource<bool> first;
        lock (_gate)
        {
            if (_live)
            {
                return true;
            }

            if (_failed)
            {
                return false;
            }

            if (_handle == null)
            {
                _first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    _handle = _stats.WatchAll(OnDocuments, OnError);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("STATS WATCH START ERROR: " + ex);
                    _failed = true;
                    _handle = null;
                    return false;
                }
            }

            first = _first!;
        }

        Task finished = await Task.WhenAny(first.Task, Task.Delay(TimeSpan.FromSeconds(ServiceHelper.TimeoutSeconds)));
        if (finished != first.Task)
        {
            return false;
        }

        return await first.Task;
    }

    public Dictionary<string, GroupActivity> Snapshot()
    {
        lock (_gate)
        {
            Dictionary<string, GroupActivity> copy = new Dictionary<string, GroupActivity>();
            foreach (KeyValuePair<string, GroupStatsDocument> pair in _docs)
            {
                copy[pair.Key] = pair.Value.ToActivity();
            }

            return copy;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _handle?.Dispose();
            _handle = null;
            _first = null;
            _docs = new Dictionary<string, GroupStatsDocument>();
            _live = false;
            _failed = false;
        }
    }

    private void OnDocuments(List<GroupStatsDocument> documents)
    {
        Dictionary<string, GroupStatsDocument> map = new Dictionary<string, GroupStatsDocument>();
        for (int i = 0; i < documents.Count; i++)
        {
            map[documents[i].Id] = documents[i];
        }

        TaskCompletionSource<bool>? first;
        lock (_gate)
        {
            _docs = map;
            _live = true;
            first = _first;
        }

        first?.TrySetResult(true);
        Changed?.Invoke();
    }

    private void OnError(Exception error)
    {
        System.Diagnostics.Debug.WriteLine("STATS WATCH ERROR: " + error);
        TaskCompletionSource<bool>? first;
        lock (_gate)
        {
            bool wasLive = _live;
            _live = false;
            _failed = !wasLive;
            first = _first;

            // After a failure the next EnsureStartedAsync attaches a fresh listener (unless it never worked).
            _handle?.Dispose();
            _handle = null;
        }

        first?.TrySetResult(false);
    }
}
