using System.Text.Encodings.Web;
using System.Text.Json;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class DirectoryService : IDirectoryService
{
    // Firestore allows about 1 MB per string field. 200 groups with long descriptions need about 100 KB.
    private const int MaxJsonLength = 900000;

    private readonly ISessionService _session;
    private readonly IDirectoryRepository _directoryRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ICommunityRepository _communityRepository;
    private readonly ISuperCommunityRepository _superRepository;

    // The relaxed encoder keeps Marathi / emoji as real characters (3-4 bytes) instead of \uXXXX escapes (6-12 bytes).
    private static readonly DirectoryJsonContext JsonContext = new DirectoryJsonContext(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    private readonly object _gate = new object();
    private DirectorySnapshot? _memberCache;
    private DirectorySnapshot? _adminCache;

    // Set when a rebuild after an admin change failed: admin screens then use the old queries (always right).
    private bool _dirty;

    // The automatic "directory is missing, build it" runs once per app session.
    private bool _autoBuildTried;

    public DirectoryService(
        ISessionService session,
        IDirectoryRepository directoryRepository,
        IGroupRepository groupRepository,
        ICommunityRepository communityRepository,
        ISuperCommunityRepository superRepository)
    {
        _session = session;
        _directoryRepository = directoryRepository;
        _groupRepository = groupRepository;
        _communityRepository = communityRepository;
        _superRepository = superRepository;
    }

    public async Task<DirectorySnapshot?> GetAsync(int maxAgeSeconds)
    {
        // The admin snapshot contains everything a member snapshot has, so a fresh one is good enough.
        DirectorySnapshot? admin;
        DirectorySnapshot? member;
        lock (_gate)
        {
            admin = _adminCache;
            member = _memberCache;
        }

        if (admin != null && _session.IsAdmin && IsFresh(admin, maxAgeSeconds) && !_dirty)
        {
            return admin;
        }

        if (member != null && IsFresh(member, maxAgeSeconds))
        {
            return member;
        }

        try
        {
            DirectoryDocument? main = await ServiceHelper.WithTimeoutAsync(_directoryRepository.GetMainAsync());
            DirectorySnapshot? snapshot = ParseMain(main, null);
            if (snapshot != null)
            {
                lock (_gate)
                {
                    _memberCache = snapshot;
                }
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DIRECTORY READ ERROR: " + ex);
            return null;
        }
    }

    public async Task<DirectorySnapshot?> GetForAdminAsync(int maxAgeSeconds)
    {
        if (!_session.IsAdmin)
        {
            return null;
        }

        if (_dirty)
        {
            // An earlier rebuild failed. Try once more; until it works the caller uses the old queries.
            await TryRebuildAsync();
            if (_dirty)
            {
                return null;
            }
        }

        DirectorySnapshot? cached;
        lock (_gate)
        {
            cached = _adminCache;
        }

        if (cached != null && IsFresh(cached, maxAgeSeconds))
        {
            return cached;
        }

        try
        {
            Task<DirectoryDocument?> mainTask = _directoryRepository.GetMainAsync();
            Task<DirectoryDocument?> adminTask = _directoryRepository.GetAdminAsync();
            await ServiceHelper.WithTimeoutAsync(Task.WhenAll(mainTask, adminTask));

            DirectoryDocument? main = mainTask.Result;
            DirectoryDocument? adminDoc = adminTask.Result;

            if (main == null || adminDoc == null)
            {
                // First use after the update (or a half-written directory): build it, once per session.
                if (_autoBuildTried)
                {
                    return null;
                }

                _autoBuildTried = true;
                await TryRebuildAsync();
                lock (_gate)
                {
                    return _dirty ? null : _adminCache;
                }
            }

            DirectoryAdminPayload? adminPayload = ParseAdminPayload(adminDoc);
            DirectorySnapshot? snapshot = ParseMain(main, adminPayload);
            if (snapshot == null || adminPayload == null)
            {
                return null;
            }

            lock (_gate)
            {
                _adminCache = snapshot;
                _memberCache = MemberViewOf(snapshot);
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DIRECTORY ADMIN READ ERROR: " + ex);
            return null;
        }
    }

    public async Task<OperationResult<DirectoryRebuildInfo>> RebuildAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<DirectoryRebuildInfo>.Fail("You do not have permission to rebuild the directory.");
        }

        try
        {
            DirectoryRebuildInfo info = await BuildAndWriteAsync();
            return OperationResult<DirectoryRebuildInfo>.Success(info);
        }
        catch (TimeoutException)
        {
            return OperationResult<DirectoryRebuildInfo>.Fail("Rebuilding the directory is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("REBUILD DIRECTORY ERROR: " + ex);
            return OperationResult<DirectoryRebuildInfo>.Fail(ServiceHelper.MapError(ex, "We could not rebuild the directory. Please try again."));
        }
    }

    public async Task TryRebuildAsync()
    {
        if (!_session.IsAdmin)
        {
            return;
        }

        try
        {
            await BuildAndWriteAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DIRECTORY REBUILD FAILED (screens use the old queries): " + ex);
            lock (_gate)
            {
                _dirty = true;
            }
        }
    }

    // ---------- Building ----------

    // Reads the source collections (the source of truth), writes both documents and refreshes the
    // in-memory copies, so the admin's own next screen needs no extra read.
    private async Task<DirectoryRebuildInfo> BuildAndWriteAsync()
    {
        Task<List<AnnouncementGroup>> groupsTask = _groupRepository.GetAllAsync();
        Task<List<Community>> communitiesTask = _communityRepository.GetAllAsync();
        Task<List<SuperCommunity>> supersTask = _superRepository.GetAllAsync();
        await ServiceHelper.WithTimeoutAsync(Task.WhenAll(groupsTask, communitiesTask, supersTask));

        List<AnnouncementGroup> groups = groupsTask.Result;
        List<Community> communities = communitiesTask.Result;
        List<SuperCommunity> supers = supersTask.Result;

        groups.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        communities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        supers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        DirectoryMainPayload main = new DirectoryMainPayload();
        DirectoryAdminPayload admin = new DirectoryAdminPayload();

        for (int i = 0; i < groups.Count; i++)
        {
            DirectoryGroupEntry entry = new DirectoryGroupEntry();
            entry.Id = groups[i].Id;
            entry.Name = groups[i].Name ?? string.Empty;
            entry.Number = groups[i].Number ?? string.Empty;
            entry.Description = groups[i].Description ?? string.Empty;
            entry.IsActive = groups[i].IsActive;

            if (entry.IsActive)
            {
                main.Groups.Add(entry);
            }
            else
            {
                admin.InactiveGroups.Add(entry);
            }
        }

        for (int i = 0; i < communities.Count; i++)
        {
            DirectoryCommunityEntry entry = new DirectoryCommunityEntry();
            entry.Id = communities[i].Id;
            entry.Name = communities[i].Name ?? string.Empty;
            entry.Number = communities[i].Number ?? string.Empty;
            entry.Description = communities[i].Description ?? string.Empty;
            entry.GroupIds = communities[i].GroupIds != null ? new List<string>(communities[i].GroupIds) : new List<string>();
            entry.SuperCommunityId = communities[i].SuperCommunityId ?? string.Empty;
            main.Communities.Add(entry);
        }

        for (int i = 0; i < supers.Count; i++)
        {
            DirectorySuperEntry entry = new DirectorySuperEntry();
            entry.Id = supers[i].Id;
            entry.Name = supers[i].Name ?? string.Empty;
            entry.Number = supers[i].Number ?? string.Empty;
            entry.Description = supers[i].Description ?? string.Empty;
            main.SuperCommunities.Add(entry);
        }

        string mainJson = JsonSerializer.Serialize(main, JsonContext.DirectoryMainPayload);
        string adminJson = JsonSerializer.Serialize(admin, JsonContext.DirectoryAdminPayload);
        if (mainJson.Length > MaxJsonLength || adminJson.Length > MaxJsonLength)
        {
            throw new InvalidOperationException("The directory is too large for one document (" + mainJson.Length + " / " + adminJson.Length + " characters).");
        }

        await ServiceHelper.WithTimeoutAsync(Task.WhenAll(
            _directoryRepository.SetMainAsync(mainJson),
            _directoryRepository.SetAdminAsync(adminJson)));

        DirectorySnapshot adminSnapshot = ToSnapshot(main, admin);
        lock (_gate)
        {
            _adminCache = adminSnapshot;
            _memberCache = MemberViewOf(adminSnapshot);
            _dirty = false;
        }

        DirectoryRebuildInfo info = new DirectoryRebuildInfo();
        info.Groups = groups.Count;
        info.Communities = communities.Count;
        info.SuperCommunities = supers.Count;
        return info;
    }

    // ---------- Parsing ----------

    private static DirectoryAdminPayload? ParseAdminPayload(DirectoryDocument document)
    {
        try
        {
            return JsonSerializer.Deserialize(document.Data, JsonContext.DirectoryAdminPayload);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DIRECTORY ADMIN PARSE ERROR: " + ex);
            return null;
        }
    }

    // Returns null when the document is missing or damaged. adminPayload != null gives an admin snapshot.
    private static DirectorySnapshot? ParseMain(DirectoryDocument? document, DirectoryAdminPayload? adminPayload)
    {
        if (document == null || string.IsNullOrEmpty(document.Data))
        {
            return null;
        }

        try
        {
            DirectoryMainPayload? main = JsonSerializer.Deserialize(document.Data, JsonContext.DirectoryMainPayload);
            if (main == null)
            {
                return null;
            }

            return ToSnapshot(main, adminPayload);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("DIRECTORY PARSE ERROR: " + ex);
            return null;
        }
    }

    private static DirectorySnapshot ToSnapshot(DirectoryMainPayload main, DirectoryAdminPayload? admin)
    {
        DirectorySnapshot snapshot = new DirectorySnapshot();
        snapshot.IncludesInactive = admin != null;
        snapshot.LoadedUtc = DateTime.UtcNow;

        AddGroups(snapshot.Groups, main.Groups, true);
        if (admin != null)
        {
            AddGroups(snapshot.Groups, admin.InactiveGroups, false);
        }

        snapshot.Groups.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < main.Communities.Count; i++)
        {
            DirectoryCommunityEntry entry = main.Communities[i];
            Community item = new Community();
            item.Id = entry.Id;
            item.Name = entry.Name;
            item.Number = entry.Number;
            item.Description = entry.Description;
            item.GroupIds = entry.GroupIds != null ? new List<string>(entry.GroupIds) : new List<string>();
            item.SuperCommunityId = entry.SuperCommunityId ?? string.Empty;
            snapshot.Communities.Add(item);
        }

        for (int i = 0; i < main.SuperCommunities.Count; i++)
        {
            DirectorySuperEntry entry = main.SuperCommunities[i];
            SuperCommunity item = new SuperCommunity();
            item.Id = entry.Id;
            item.Name = entry.Name;
            item.Number = entry.Number;
            item.Description = entry.Description;
            snapshot.SuperCommunities.Add(item);
        }

        snapshot.Communities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        snapshot.SuperCommunities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return snapshot;
    }

    private static void AddGroups(List<AnnouncementGroup> target, List<DirectoryGroupEntry> source, bool active)
    {
        for (int i = 0; i < source.Count; i++)
        {
            AnnouncementGroup group = new AnnouncementGroup();
            group.Id = source[i].Id;
            group.Name = source[i].Name;
            group.Number = source[i].Number;
            group.Description = source[i].Description;
            group.IsActive = active;
            target.Add(group);
        }
    }

    // The member view of an admin snapshot: the same lists without the inactive groups.
    private static DirectorySnapshot MemberViewOf(DirectorySnapshot admin)
    {
        DirectorySnapshot view = new DirectorySnapshot();
        view.IncludesInactive = false;
        view.LoadedUtc = admin.LoadedUtc;
        view.Groups = admin.CopyGroups(true);
        view.Communities = admin.CopyCommunities();
        view.SuperCommunities = admin.CopySuperCommunities();
        return view;
    }

    private static bool IsFresh(DirectorySnapshot snapshot, int maxAgeSeconds)
    {
        return maxAgeSeconds > 0 && (DateTime.UtcNow - snapshot.LoadedUtc).TotalSeconds < maxAgeSeconds;
    }
}
