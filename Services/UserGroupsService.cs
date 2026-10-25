using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class UserGroupsService : IUserGroupsService
{
    private readonly IUserGroupsRepository _userGroups;
    private readonly IMembershipRepository _memberships;

    // Memory copy per user (the signed-in member's own id, and ids of users an admin just changed).
    private readonly Dictionary<string, List<string>> _known = new Dictionary<string, List<string>>();
    private readonly object _gate = new object();

    public UserGroupsService(IUserGroupsRepository userGroups, IMembershipRepository memberships)
    {
        _userGroups = userGroups;
        _memberships = memberships;
    }

    public async Task<List<string>> GetIdsAsync(string uid)
    {
        List<string>? cached = Copy(uid);
        if (cached != null)
        {
            return cached;
        }

        try
        {
            List<string>? stored = await ServiceHelper.WithTimeoutAsync(_userGroups.GetAsync(uid));
            if (stored == null)
            {
                return await ReconcileAsync(uid);
            }

            Remember(uid, stored);
            return new List<string>(stored);
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            // For example the new rules are not deployed yet: the old collection group query still works,
            // so the app keeps working (just with more reads). Nothing is remembered.
            System.Diagnostics.Debug.WriteLine("USER GROUPS READ FAILED, USING MEMBERS QUERY: " + ex.Message);
            return await ServiceHelper.WithTimeoutAsync(_memberships.GetGroupIdsForUserAsync(uid));
        }
    }

    public async Task AddAsync(string uid, string groupId)
    {
        // The document is read from the server on purpose (not from memory): another device or an admin may have changed it.
        List<string>? stored = await ServiceHelper.WithTimeoutAsync(_userGroups.GetAsync(uid));
        if (stored == null)
        {
            // No document yet: the members collection group already contains the new membership.
            await ReconcileAsync(uid);
            return;
        }

        if (stored.Contains(groupId))
        {
            Remember(uid, stored);
            return;
        }

        stored.Add(groupId);
        await ServiceHelper.WithTimeoutAsync(_userGroups.SetAsync(uid, stored));
        Remember(uid, stored);
    }

    public async Task RemoveAsync(string uid, string groupId)
    {
        List<string>? stored = await ServiceHelper.WithTimeoutAsync(_userGroups.GetAsync(uid));
        if (stored == null)
        {
            // No document yet: the members collection group no longer contains the membership.
            await ReconcileAsync(uid);
            return;
        }

        if (!stored.Remove(groupId))
        {
            Remember(uid, stored);
            return;
        }

        await ServiceHelper.WithTimeoutAsync(_userGroups.SetAsync(uid, stored));
        Remember(uid, stored);
    }

    public async Task<List<string>> ReconcileAsync(string uid)
    {
        List<string> truth = await ServiceHelper.WithTimeoutAsync(_memberships.GetGroupIdsForUserAsync(uid));

        List<string> ids = new List<string>();
        for (int i = 0; i < truth.Count; i++)
        {
            if (!ids.Contains(truth[i]))
            {
                ids.Add(truth[i]);
            }
        }

        await ServiceHelper.WithTimeoutAsync(_userGroups.SetAsync(uid, ids));
        Remember(uid, ids);
        return new List<string>(ids);
    }

    public void SetKnown(string uid, List<string> groupIds)
    {
        Remember(uid, groupIds);
    }

    public void Clear()
    {
        lock (_gate)
        {
            _known.Clear();
        }
    }

    private List<string>? Copy(string uid)
    {
        lock (_gate)
        {
            List<string>? list;
            if (_known.TryGetValue(uid, out list))
            {
                return new List<string>(list);
            }

            return null;
        }
    }

    private void Remember(string uid, List<string> ids)
    {
        lock (_gate)
        {
            _known[uid] = new List<string>(ids);
        }
    }
}
