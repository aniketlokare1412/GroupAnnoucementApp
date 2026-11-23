using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class CommunityAdminService : ICommunityAdminService
{
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly ICommunityRepository _communityRepository;
    private readonly ISuperCommunityRepository _superRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IDirectoryService _directory;

    // Same value as the group lists: shorter than Home's 20 second refresh.
    private const int DirectoryMaxAgeSeconds = 15;

    public CommunityAdminService(
        ISessionService session,
        IAuthService authService,
        ICommunityRepository communityRepository,
        ISuperCommunityRepository superRepository,
        IGroupRepository groupRepository,
        IDirectoryService directory)
    {
        _session = session;
        _authService = authService;
        _communityRepository = communityRepository;
        _superRepository = superRepository;
        _groupRepository = groupRepository;
        _directory = directory;
    }

    // ---------- Reading ----------

    public async Task<OperationResult<List<Community>>> GetAllCommunitiesAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<Community>>.Fail("You do not have permission to view AB Switches.");
        }

        try
        {
            // Phase 1: from the directory (1-2 reads) when it exists, otherwise the old query.
            // The edit screens still read one community by id (GetCommunityAsync) from the source.
            DirectorySnapshot? snapshot = await _directory.GetForAdminAsync(DirectoryMaxAgeSeconds);
            List<Community> items = snapshot != null
                ? snapshot.CopyCommunities()
                : await ServiceHelper.WithTimeoutAsync(_communityRepository.GetAllAsync());
            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return OperationResult<List<Community>>.Success(items);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<Community>>.Fail("Loading AB Switches is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD COMMUNITIES ERROR: " + ex);
            return OperationResult<List<Community>>.Fail(ServiceHelper.MapError(ex, "We could not load the AB Switches. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<Community?>> GetCommunityAsync(string id)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<Community?>.Fail("You do not have permission to view AB Switches.");
        }

        if (string.IsNullOrEmpty(id))
        {
            return OperationResult<Community?>.Fail("Unknown AB Switch.");
        }

        try
        {
            Community? item = await ServiceHelper.WithTimeoutAsync(_communityRepository.GetByIdAsync(id));
            if (item == null)
            {
                return OperationResult<Community?>.Fail("This AB Switch no longer exists.");
            }

            if (item.GroupIds == null)
            {
                item.GroupIds = new List<string>();
            }

            return OperationResult<Community?>.Success(item);
        }
        catch (TimeoutException)
        {
            return OperationResult<Community?>.Fail("Loading the AB Switch is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD COMMUNITY ERROR: " + ex);
            return OperationResult<Community?>.Fail(ServiceHelper.MapError(ex, "We could not load the AB Switch. Please try again."));
        }
    }

    public async Task<OperationResult<List<SuperCommunity>>> GetAllSuperCommunitiesAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<SuperCommunity>>.Fail("You do not have permission to view Feeders.");
        }

        try
        {
            DirectorySnapshot? snapshot = await _directory.GetForAdminAsync(DirectoryMaxAgeSeconds);
            List<SuperCommunity> items = snapshot != null
                ? snapshot.CopySuperCommunities()
                : await ServiceHelper.WithTimeoutAsync(_superRepository.GetAllAsync());
            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return OperationResult<List<SuperCommunity>>.Success(items);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<SuperCommunity>>.Fail("Loading Feeders is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD SUPER COMMUNITIES ERROR: " + ex);
            return OperationResult<List<SuperCommunity>>.Fail(ServiceHelper.MapError(ex, "We could not load the Feeders. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<SuperCommunity?>> GetSuperCommunityAsync(string id)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<SuperCommunity?>.Fail("You do not have permission to view Feeders.");
        }

        if (string.IsNullOrEmpty(id))
        {
            return OperationResult<SuperCommunity?>.Fail("Unknown Feeder.");
        }

        try
        {
            SuperCommunity? item = await ServiceHelper.WithTimeoutAsync(_superRepository.GetByIdAsync(id));
            if (item == null)
            {
                return OperationResult<SuperCommunity?>.Fail("This Feeder no longer exists.");
            }

            return OperationResult<SuperCommunity?>.Success(item);
        }
        catch (TimeoutException)
        {
            return OperationResult<SuperCommunity?>.Fail("Loading the Feeder is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD SUPER COMMUNITY ERROR: " + ex);
            return OperationResult<SuperCommunity?>.Fail(ServiceHelper.MapError(ex, "We could not load the Feeder. Please try again."));
        }
    }

    // ---------- Communities ----------

    public async Task<OperationResult> CreateCommunityAsync(string name, string description, string number, List<string> groupIds, string superCommunityId)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();
        string superId = superCommunityId ?? string.Empty;

        string? problem = CheckCanModify("AB Switches") ?? ValidateFields("AB Switch", cleanName, cleanDescription, cleanNumber);
        if (problem == null && string.IsNullOrEmpty(superId))
        {
            problem = "Please choose a Feeder.";
        }

        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        string? uid = _authService.GetCurrentUserId();
        if (string.IsNullOrEmpty(uid))
        {
            return OperationResult.Fail("You are signed out. Please sign in again.");
        }

        try
        {
            List<Community> existing = await ServiceHelper.WithTimeoutAsync(_communityRepository.GetAllAsync());
            // Name and number only have to be unique inside the same Feeder.
            problem = CheckDuplicates("AB Switch", ToKeys(InFeeder(existing, superId)), cleanName, cleanNumber, null, " in this Feeder");
            if (problem == null)
            {
                problem = await CheckSuperExistsAsync(superId);
            }

            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            List<string> validGroupIds = await KeepExistingGroupIdsAsync(groupIds);

            problem = await CheckDtcNamesInFeederAsync(superId, null, validGroupIds, existing);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            Community community = new Community();
            community.Name = cleanName;
            community.Description = cleanDescription;
            community.Number = cleanNumber;
            community.GroupIds = validGroupIds;
            community.SuperCommunityId = superId;
            community.CreatedBy = uid;

            await ServiceHelper.WithTimeoutAsync(_communityRepository.CreateAsync(Guid.NewGuid().ToString("N"), community));
            await _directory.TryRebuildAsync();
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the AB Switches list to see whether it was created.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CREATE COMMUNITY ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not create the AB Switch. Please try again."));
        }
    }

    public async Task<OperationResult> UpdateCommunityAsync(string id, string name, string description, string number, List<string> groupIds, string superCommunityId)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();
        string superId = superCommunityId ?? string.Empty;

        string? problem = CheckCanModify("AB Switches");
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown AB Switch.";
        }

        if (problem == null)
        {
            problem = ValidateFields("AB Switch", cleanName, cleanDescription, cleanNumber);
        }

        if (problem == null && string.IsNullOrEmpty(superId))
        {
            problem = "Please choose a Feeder.";
        }

        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        try
        {
            List<Community> existing = await ServiceHelper.WithTimeoutAsync(_communityRepository.GetAllAsync());
            if (!ContainsId(existing, id))
            {
                return OperationResult.Fail("This AB Switch no longer exists.");
            }

            // Name and number only have to be unique inside the same Feeder.
            problem = CheckDuplicates("AB Switch", ToKeys(InFeeder(existing, superId)), cleanName, cleanNumber, id, " in this Feeder");
            if (problem == null)
            {
                problem = await CheckSuperExistsAsync(superId);
            }

            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            List<string> validGroupIds = await KeepExistingGroupIdsAsync(groupIds);

            problem = await CheckDtcNamesInFeederAsync(superId, id, validGroupIds, existing);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            await ServiceHelper.WithTimeoutAsync(_communityRepository.UpdateAsync(id, cleanName, cleanDescription, cleanNumber, validGroupIds, superId));
            await _directory.TryRebuildAsync();
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the AB Switches list to see the current details.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE COMMUNITY ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not save the AB Switch. Please try again."));
        }
    }

    // ---------- Super communities ----------

    public async Task<OperationResult> CreateSuperCommunityAsync(string name, string description, string number, List<string> communityIds)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();

        string? problem = CheckCanModify("Feeders") ?? ValidateFields("Feeder", cleanName, cleanDescription, cleanNumber);
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        string? uid = _authService.GetCurrentUserId();
        if (string.IsNullOrEmpty(uid))
        {
            return OperationResult.Fail("You are signed out. Please sign in again.");
        }

        try
        {
            List<SuperCommunity> existing = await ServiceHelper.WithTimeoutAsync(_superRepository.GetAllAsync());
            problem = CheckDuplicates("Feeder", ToKeys(existing), cleanName, cleanNumber, null);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            problem = await CheckFeederContentsAsync(null, communityIds);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            SuperCommunity item = new SuperCommunity();
            item.Name = cleanName;
            item.Description = cleanDescription;
            item.Number = cleanNumber;
            item.CreatedBy = uid;

            string id = Guid.NewGuid().ToString("N");
            await ServiceHelper.WithTimeoutAsync(_superRepository.CreateAsync(id, item));

            // The super community exists now, so the communities can point at it.
            return await ApplyLinksAndRebuildDirectoryAsync(id, communityIds);
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the Feeders list to see whether it was created.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CREATE SUPER COMMUNITY ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not create the Feeder. Please try again."));
        }
    }

    public async Task<OperationResult> UpdateSuperCommunityAsync(string id, string name, string description, string number, List<string> communityIds)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();

        string? problem = CheckCanModify("Feeders");
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown Feeder.";
        }

        if (problem == null)
        {
            problem = ValidateFields("Feeder", cleanName, cleanDescription, cleanNumber);
        }

        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        try
        {
            List<SuperCommunity> existing = await ServiceHelper.WithTimeoutAsync(_superRepository.GetAllAsync());
            bool found = false;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i].Id == id)
                {
                    found = true;
                }
            }

            if (!found)
            {
                return OperationResult.Fail("This Feeder no longer exists.");
            }

            problem = CheckDuplicates("Feeder", ToKeys(existing), cleanName, cleanNumber, id);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            problem = await CheckFeederContentsAsync(id, communityIds);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            await ServiceHelper.WithTimeoutAsync(_superRepository.UpdateDetailsAsync(id, cleanName, cleanDescription, cleanNumber));
            return await ApplyLinksAndRebuildDirectoryAsync(id, communityIds);
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the Feeders list to see the current details.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE SUPER COMMUNITY ERROR: " + ex);
            return OperationResult.Fail(ServiceHelper.MapError(ex, "We could not save the Feeder. Please try again."));
        }
    }

    // The links change communities, so the directory is rebuilt afterwards even when some links failed
    // (or an error is thrown): it always mirrors what is really stored.
    private async Task<OperationResult> ApplyLinksAndRebuildDirectoryAsync(string superId, List<string> communityIds)
    {
        try
        {
            return await ApplyCommunityLinksAsync(superId, communityIds);
        }
        finally
        {
            await _directory.TryRebuildAsync();
        }
    }

    // Makes exactly the chosen communities point at this super community. Communities that
    // were removed from the list are cleared; communities that were under another super
    // community are moved. Only communities whose link changes are written.
    private async Task<OperationResult> ApplyCommunityLinksAsync(string superId, List<string> communityIds)
    {
        HashSet<string> wanted = new HashSet<string>(communityIds ?? new List<string>());
        List<Community> all = await ServiceHelper.WithTimeoutAsync(_communityRepository.GetAllAsync());

        int failed = 0;
        for (int i = 0; i < all.Count; i++)
        {
            Community community = all[i];
            bool shouldBeIn = wanted.Contains(community.Id);
            bool isIn = community.SuperCommunityId == superId;

            // A community always keeps a super community: communities are only ever moved to this one, never cleared.
            string? newValue = null;
            if (shouldBeIn && !isIn)
            {
                newValue = superId;
            }

            if (newValue == null)
            {
                continue;
            }

            try
            {
                await ServiceHelper.WithTimeoutAsync(_communityRepository.SetSuperCommunityAsync(community.Id, newValue));
            }
            catch (Exception ex)
            {
                failed++;
                System.Diagnostics.Debug.WriteLine("LINK COMMUNITY ERROR: " + ex);
            }
        }

        if (failed > 0)
        {
            return OperationResult.Fail("The Feeder was saved, but " + failed + " AB Switch link(s) could not be updated. Please open it again and check the list.");
        }

        return OperationResult.Success();
    }

    // ---------- Checks ----------

    // The AB Switches that sit under one Feeder. Name and number rules only compare these.
    private static List<Community> InFeeder(List<Community> all, string superId)
    {
        List<Community> inside = new List<Community>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].SuperCommunityId == superId)
            {
                inside.Add(all[i]);
            }
        }

        return inside;
    }

    // Under one Feeder two different DTCs may not share a name (the same name is fine under another Feeder).
    // Looks at this AB Switch's DTCs together with the DTCs of the other AB Switches of the same Feeder.
    private async Task<string?> CheckDtcNamesInFeederAsync(string superId, string? thisCommunityId, List<string> thisGroupIds, List<Community> all)
    {
        HashSet<string> ids = new HashSet<string>(thisGroupIds);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].SuperCommunityId == superId && all[i].Id != thisCommunityId)
            {
                for (int j = 0; j < all[i].GroupIds.Count; j++)
                {
                    ids.Add(all[i].GroupIds[j]);
                }
            }
        }

        return await CheckDtcNamesAsync(ids);
    }

    private async Task<string?> CheckDtcNamesAsync(HashSet<string> groupIds)
    {
        List<AnnouncementGroup> groups = await ServiceHelper.WithTimeoutAsync(_groupRepository.GetAllAsync());
        Dictionary<string, string> seen = new Dictionary<string, string>();
        for (int i = 0; i < groups.Count; i++)
        {
            if (!groupIds.Contains(groups[i].Id))
            {
                continue;
            }

            string key = groups[i].Name.Trim().ToLowerInvariant();
            string? otherId;
            if (seen.TryGetValue(key, out otherId) && otherId != groups[i].Id)
            {
                return "Two DTCs named \"" + groups[i].Name + "\" would be in the same Feeder. Rename one of them first.";
            }

            seen[key] = groups[i].Id;
        }

        return null;
    }

    // What a Feeder would contain after saving: its current AB Switches plus the ones being moved in.
    // Names and numbers of the AB Switches, and the names of their DTCs, must not repeat inside it.
    private async Task<string?> CheckFeederContentsAsync(string? superId, List<string>? communityIds)
    {
        List<Community> all = await ServiceHelper.WithTimeoutAsync(_communityRepository.GetAllAsync());
        HashSet<string> wanted = new HashSet<string>(communityIds ?? new List<string>());

        List<Community> inside = new List<Community>();
        for (int i = 0; i < all.Count; i++)
        {
            if ((superId != null && all[i].SuperCommunityId == superId) || wanted.Contains(all[i].Id))
            {
                inside.Add(all[i]);
            }
        }

        for (int i = 0; i < inside.Count; i++)
        {
            for (int j = i + 1; j < inside.Count; j++)
            {
                if (string.Equals(inside[i].Name.Trim(), inside[j].Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return "Two AB Switches named \"" + inside[i].Name + "\" would be in this Feeder. Rename one of them first.";
                }

                if (string.Equals(inside[i].Number.Trim(), inside[j].Number.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return "Two AB Switches would use the number " + inside[i].Number + " in this Feeder. Change one of them first.";
                }
            }
        }

        HashSet<string> groupIds = new HashSet<string>();
        for (int i = 0; i < inside.Count; i++)
        {
            for (int j = 0; j < inside[i].GroupIds.Count; j++)
            {
                groupIds.Add(inside[i].GroupIds[j]);
            }
        }

        return await CheckDtcNamesAsync(groupIds);
    }

    private string? CheckCanModify(string what)
    {
        if (!_session.IsAdmin)
        {
            return "You do not have permission to change " + what + ".";
        }

        // Without a connection Firestore would queue the write and apply it later,
        // which is confusing for an admin action. So we refuse up front.
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return "You are offline. Please connect to the internet and try again.";
        }

        return null;
    }

    private static string? ValidateFields(string noun, string name, string description, string number)
    {
        if (name.Length == 0)
        {
            return "Please enter a " + noun + " name.";
        }

        if (name.Length > CommunityLimits.MaxNameLength)
        {
            return "The " + noun + " name can be at most " + CommunityLimits.MaxNameLength + " characters.";
        }

        if (description.Length > CommunityLimits.MaxDescriptionLength)
        {
            return "The description can be at most " + CommunityLimits.MaxDescriptionLength + " characters.";
        }

        if (number.Length == 0)
        {
            return "Please enter a " + noun + " number.";
        }

        if (number.Length > CommunityLimits.MaxNumberLength)
        {
            return "The " + noun + " number can have at most " + CommunityLimits.MaxNumberLength + " digits.";
        }

        for (int i = 0; i < number.Length; i++)
        {
            if (number[i] < '0' || number[i] > '9')
            {
                return "The " + noun + " number must be a whole number, like 12.";
            }
        }

        if (number[0] == '0')
        {
            return "The " + noun + " number must be a positive number without a leading zero.";
        }

        return null;
    }

    // Names and numbers must be unique within the same kind, ignoring upper/lower case.
    private static string? CheckDuplicates(string noun, List<(string Id, string Name, string Number)> others, string name, string number, string? excludeId, string scope = "")
    {
        for (int i = 0; i < others.Count; i++)
        {
            if (excludeId != null && others[i].Id == excludeId)
            {
                continue;
            }

            if (string.Equals(others[i].Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return "A " + noun + " named \"" + others[i].Name + "\" already exists" + scope + ".";
            }

            if (string.Equals(others[i].Number.Trim(), number, StringComparison.OrdinalIgnoreCase))
            {
                return "Another " + noun + " already uses the number " + number + scope + ".";
            }
        }

        return null;
    }

    private static List<(string Id, string Name, string Number)> ToKeys(List<Community> items)
    {
        List<(string Id, string Name, string Number)> keys = new List<(string Id, string Name, string Number)>();
        for (int i = 0; i < items.Count; i++)
        {
            keys.Add((items[i].Id, items[i].Name, items[i].Number));
        }

        return keys;
    }

    private static List<(string Id, string Name, string Number)> ToKeys(List<SuperCommunity> items)
    {
        List<(string Id, string Name, string Number)> keys = new List<(string Id, string Name, string Number)>();
        for (int i = 0; i < items.Count; i++)
        {
            keys.Add((items[i].Id, items[i].Name, items[i].Number));
        }

        return keys;
    }

    private static bool ContainsId(List<Community> items, string id)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    // Returns a message when a non-empty super community id does not exist.
    private async Task<string?> CheckSuperExistsAsync(string superId)
    {
        if (string.IsNullOrEmpty(superId))
        {
            return null;
        }

        SuperCommunity? found = await ServiceHelper.WithTimeoutAsync(_superRepository.GetByIdAsync(superId));
        return found == null ? "The chosen Feeder no longer exists." : null;
    }

    // Drops duplicates and ids of groups that do not exist, so a stale picker cannot save junk.
    private async Task<List<string>> KeepExistingGroupIdsAsync(List<string> groupIds)
    {
        List<AnnouncementGroup> groups = await ServiceHelper.WithTimeoutAsync(_groupRepository.GetAllAsync());
        HashSet<string> known = new HashSet<string>();
        for (int i = 0; i < groups.Count; i++)
        {
            known.Add(groups[i].Id);
        }

        List<string> result = new List<string>();
        HashSet<string> seen = new HashSet<string>();
        List<string> source = groupIds ?? new List<string>();
        for (int i = 0; i < source.Count; i++)
        {
            if (known.Contains(source[i]) && seen.Add(source[i]))
            {
                result.Add(source[i]);
            }
        }

        return result;
    }
}
