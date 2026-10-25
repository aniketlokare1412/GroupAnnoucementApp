using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;

namespace GroupAnnouncementApp.Services;

// Phase 5. Admin screens (Users tab, "add members") need every user, and reading them costs one read per user
// (5,000 users = 5,000 reads each time, 10% of the free daily quota). The list is therefore kept in memory and
// re-read only when it is older than CacheMinutes or when the admin asks for it (pull-to-refresh).
// Changes the admin makes (active / role) update the cached list at once, so the screens stay right.
// New registrations appear after the cache expires or on pull-to-refresh.
public interface IUserListCache
{
    // A copy of the list. Throws when the read fails (callers already handle errors).
    Task<List<UserProfile>> GetAllAsync(bool forceRefresh);

    // A profile from the cache when the cache is fresh (no read), otherwise null.
    UserProfile? TryGetFresh(string uid);

    // Applies an admin's change to the cached profile.
    void Patch(string uid, bool? isActive, string? userType);

    // How many users the last full read found (kept on the phone), null when never read.
    int? LastKnownCount { get; }
}

public sealed class UserListCache : IUserListCache
{
    // Raise it to read less often, lower it to see new registrations sooner.
    private const int CacheMinutes = 30;

    private const string CountKey = "users_count";

    private readonly IUserRepository _users;
    private readonly object _gate = new object();

    private List<UserProfile>? _list;
    private Dictionary<string, UserProfile> _byId = new Dictionary<string, UserProfile>();
    private DateTime _loadedUtc = DateTime.MinValue;
    private Task<List<UserProfile>>? _loading;

    public UserListCache(IUserRepository users)
    {
        _users = users;
    }

    public int? LastKnownCount
    {
        get
        {
            int count = Preferences.Default.Get(CountKey, -1);
            return count >= 0 ? count : null;
        }
    }

    public async Task<List<UserProfile>> GetAllAsync(bool forceRefresh)
    {
        Task<List<UserProfile>> loading;
        lock (_gate)
        {
            if (!forceRefresh && _list != null && IsFresh())
            {
                return new List<UserProfile>(_list);
            }

            // Two screens asking at the same moment share one read.
            if (_loading == null)
            {
                _loading = LoadAsync();
            }

            loading = _loading;
        }

        List<UserProfile> loaded = await loading;
        return new List<UserProfile>(loaded);
    }

    public UserProfile? TryGetFresh(string uid)
    {
        lock (_gate)
        {
            if (_list == null || !IsFresh())
            {
                return null;
            }

            UserProfile? found;
            return _byId.TryGetValue(uid, out found) ? found : null;
        }
    }

    public void Patch(string uid, bool? isActive, string? userType)
    {
        lock (_gate)
        {
            UserProfile? found;
            if (_byId.TryGetValue(uid, out found))
            {
                if (isActive != null)
                {
                    found.IsActive = isActive.Value;
                }

                if (userType != null)
                {
                    found.UserType = userType;
                }
            }
        }
    }

    private bool IsFresh()
    {
        return (DateTime.UtcNow - _loadedUtc).TotalMinutes < CacheMinutes;
    }

    private async Task<List<UserProfile>> LoadAsync()
    {
        try
        {
            List<UserProfile> users = await _users.GetAllAsync();
            lock (_gate)
            {
                _list = users;
                _byId = new Dictionary<string, UserProfile>();
                for (int i = 0; i < users.Count; i++)
                {
                    _byId[users[i].Id] = users[i];
                }

                _loadedUtc = DateTime.UtcNow;
            }

            Preferences.Default.Set(CountKey, users.Count);
            return users;
        }
        finally
        {
            lock (_gate)
            {
                _loading = null;
            }
        }
    }
}
