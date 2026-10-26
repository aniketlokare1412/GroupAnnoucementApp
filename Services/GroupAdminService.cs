using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;
using Microsoft.Maui.Networking;

namespace GroupAnnouncementApp.Services;

public class GroupAdminService : IGroupAdminService
{
    private const int TimeoutSeconds = 20;

    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IGroupRepository _groupRepository;
    private readonly IDirectoryService _directory;
    private readonly ICommunityRepository _communityRepository;

    // How long the admin lists may be served from memory. Shorter than Home's 20 second refresh,
    // so every refresh still sees other admins' changes.
    private const int DirectoryMaxAgeSeconds = 15;

    public GroupAdminService(ISessionService session, IAuthService authService, IGroupRepository groupRepository, IDirectoryService directory, ICommunityRepository communityRepository)
    {
        _session = session;
        _authService = authService;
        _groupRepository = groupRepository;
        _directory = directory;
        _communityRepository = communityRepository;
    }

    public async Task<OperationResult<List<AnnouncementGroup>>> GetAllGroupsAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("You do not have permission to view DTCs.");
        }

        try
        {
            // Phase 1: one or two directory reads instead of one read per group. Falls back to the
            // old query when the directory does not exist yet or cannot be read.
            DirectorySnapshot? snapshot = await _directory.GetForAdminAsync(DirectoryMaxAgeSeconds);
            List<AnnouncementGroup> groups = snapshot != null
                ? snapshot.CopyGroups(false)
                : await WithTimeoutAsync(_groupRepository.GetAllAsync());
            groups.Sort(CompareByName);
            return OperationResult<List<AnnouncementGroup>>.Success(groups);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("Loading DTCs is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD GROUPS ERROR: " + ex);
            return OperationResult<List<AnnouncementGroup>>.Fail(MapError(ex, "We could not load the DTCs. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<AnnouncementGroup?>> GetGroupAsync(string id)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<AnnouncementGroup?>.Fail("You do not have permission to view DTCs.");
        }

        if (string.IsNullOrEmpty(id))
        {
            return OperationResult<AnnouncementGroup?>.Fail("Unknown DTC.");
        }

        try
        {
            AnnouncementGroup? group = await WithTimeoutAsync(_groupRepository.GetByIdAsync(id));
            if (group == null)
            {
                return OperationResult<AnnouncementGroup?>.Fail("This DTC no longer exists.");
            }

            return OperationResult<AnnouncementGroup?>.Success(group);
        }
        catch (TimeoutException)
        {
            return OperationResult<AnnouncementGroup?>.Fail("Loading the DTC is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD GROUP ERROR: " + ex);
            return OperationResult<AnnouncementGroup?>.Fail(MapError(ex, "We could not load the DTC. Please try again."));
        }
    }

    public async Task<OperationResult> CreateGroupAsync(string name, string description, string number)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();

        string? problem = CheckCanModify();
        if (problem == null)
        {
            problem = ValidateText(cleanName, cleanDescription) ?? ValidateNumber(cleanNumber);
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
            // Read the current groups first: needed for the duplicate checks.
            List<AnnouncementGroup> existing = await WithTimeoutAsync(_groupRepository.GetAllAsync());

            problem = CheckDuplicateName(existing, cleanName, null) ?? CheckDuplicateNumber(existing, cleanNumber, null);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            AnnouncementGroup group = new AnnouncementGroup();
            group.Name = cleanName;
            group.Description = cleanDescription;
            group.Number = cleanNumber;
            group.IsActive = true;
            group.CreatedBy = uid;

            string id = Guid.NewGuid().ToString("N");
            await WithTimeoutAsync(_groupRepository.CreateAsync(id, group));
            await _directory.TryRebuildAsync();
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the DTCs list to see whether the DTC was created.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CREATE GROUP ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not create the DTC. Please try again."));
        }
    }

    // Creates the group and, when communities are chosen, adds the new group to each of them.
    public async Task<OperationResult<string?>> CreateGroupAsync(string name, string description, string number, List<string> communityIds)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();

        string? problem = CheckCanModify();
        if (problem == null)
        {
            problem = ValidateText(cleanName, cleanDescription) ?? ValidateNumber(cleanNumber);
        }
        if (problem != null)
        {
            return OperationResult<string?>.Fail(problem);
        }

        string? uid = _authService.GetCurrentUserId();
        if (string.IsNullOrEmpty(uid))
        {
            return OperationResult<string?>.Fail("You are signed out. Please sign in again.");
        }

        try
        {
            List<AnnouncementGroup> existing = await WithTimeoutAsync(_groupRepository.GetAllAsync());

            problem = CheckDuplicateName(existing, cleanName, null) ?? CheckDuplicateNumber(existing, cleanNumber, null);
            if (problem != null)
            {
                return OperationResult<string?>.Fail(problem);
            }

            AnnouncementGroup group = new AnnouncementGroup();
            group.Name = cleanName;
            group.Description = cleanDescription;
            group.Number = cleanNumber;
            group.IsActive = true;
            group.CreatedBy = uid;

            string id = Guid.NewGuid().ToString("N");
            await WithTimeoutAsync(_groupRepository.CreateAsync(id, group));

            // The group exists now. Adding it to the communities is a second step: if it fails the group is kept
            // and the admin is told which communities still need it.
            string? warning = await AddToCommunitiesAsync(id, communityIds);

            await _directory.TryRebuildAsync();
            return OperationResult<string?>.Success(warning);
        }
        catch (TimeoutException)
        {
            return OperationResult<string?>.Fail("The request is taking too long. Check the DTCs list to see whether the DTC was created.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CREATE GROUP ERROR: " + ex);
            return OperationResult<string?>.Fail(MapError(ex, "We could not create the DTC. Please try again."));
        }
    }

    // Returns null when every community was updated, otherwise a short sentence naming the ones that were not.
    private async Task<string?> AddToCommunitiesAsync(string groupId, List<string>? communityIds)
    {
        List<string> wanted = new List<string>();
        if (communityIds != null)
        {
            for (int i = 0; i < communityIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(communityIds[i]) && !wanted.Contains(communityIds[i]))
                {
                    wanted.Add(communityIds[i]);
                }
            }
        }

        if (wanted.Count == 0)
        {
            return null;
        }

        List<string> failed = new List<string>();
        List<Community> all;
        try
        {
            all = await WithTimeoutAsync(_communityRepository.GetAllAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("ADD GROUP TO COMMUNITIES (LOAD) ERROR: " + ex);
            return "The DTC was created, but it could not be added to the AB Switches. Open the AB Switches and add it there.";
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            Community? community = null;
            for (int j = 0; j < all.Count; j++)
            {
                if (all[j].Id == wanted[i])
                {
                    community = all[j];
                    break;
                }
            }

            if (community == null)
            {
                failed.Add("an AB Switch that no longer exists");
                continue;
            }

            try
            {
                List<string> groupIds = new List<string>(community.GroupIds);
                if (!groupIds.Contains(groupId))
                {
                    // The rules allow at most 500 groups in one community.
                    if (groupIds.Count >= 500)
                    {
                        failed.Add(community.Name);
                        continue;
                    }

                    groupIds.Add(groupId);
                }

                await WithTimeoutAsync(_communityRepository.UpdateAsync(community.Id, community.Name, community.Description, community.Number, groupIds, community.SuperCommunityId));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ADD GROUP TO COMMUNITY ERROR: " + ex);
                failed.Add(community.Name);
            }
        }

        if (failed.Count == 0)
        {
            return null;
        }

        return "The DTC was created, but it could not be added to: " + string.Join(", ", failed) + ". Open those AB Switches and add it there.";
    }

    public async Task<OperationResult> UpdateGroupAsync(string id, string name, string description, string number)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();
        string cleanNumber = (number ?? string.Empty).Trim();

        string? problem = CheckCanModify();
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown DTC.";
        }
        if (problem == null)
        {
            problem = ValidateText(cleanName, cleanDescription) ?? ValidateNumber(cleanNumber);
        }
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        try
        {
            List<AnnouncementGroup> existing = await WithTimeoutAsync(_groupRepository.GetAllAsync());

            if (FindById(existing, id) == null)
            {
                return OperationResult.Fail("This DTC no longer exists.");
            }

            problem = CheckDuplicateName(existing, cleanName, id) ?? CheckDuplicateNumber(existing, cleanNumber, id);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            await WithTimeoutAsync(_groupRepository.UpdateDetailsAsync(id, cleanName, cleanDescription, cleanNumber));
            await _directory.TryRebuildAsync();
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the DTCs list to see the current details.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE GROUP ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not save the DTC. Please try again."));
        }
    }

    public async Task<OperationResult> SetActiveAsync(string id, bool isActive)
    {
        string? problem = CheckCanModify();
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown DTC.";
        }
        if (problem != null)
        {
            return OperationResult.Fail(problem);
        }

        try
        {
            List<AnnouncementGroup> existing = await WithTimeoutAsync(_groupRepository.GetAllAsync());

            AnnouncementGroup? group = FindById(existing, id);
            if (group == null)
            {
                return OperationResult.Fail("This DTC no longer exists.");
            }

            await WithTimeoutAsync(_groupRepository.SetActiveAsync(id, isActive));
            await _directory.TryRebuildAsync();
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Refresh the list to see the current status.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("SET GROUP ACTIVE ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not save the change. Please try again."));
        }
    }

    // Returns null when the change is allowed, otherwise a friendly message.
    private string? CheckCanModify()
    {
        if (!_session.IsAdmin)
        {
            return "You do not have permission to change DTCs.";
        }

        // Without a connection Firestore would queue the write and apply it later,
        // which is confusing for an admin action. So we refuse up front.
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return "You are offline. Please connect to the internet and try again.";
        }

        return null;
    }

    private static string? ValidateText(string name, string description)
    {
        if (name.Length == 0)
        {
            return "Please enter a DTC name.";
        }

        if (name.Length > GroupLimits.MaxNameLength)
        {
            return "The DTC name can be at most " + GroupLimits.MaxNameLength + " characters.";
        }

        if (description.Length > GroupLimits.MaxDescriptionLength)
        {
            return "The description can be at most " + GroupLimits.MaxDescriptionLength + " characters.";
        }

        return null;
    }

    // The group number is required and must be a positive whole number.
    private static string? ValidateNumber(string number)
    {
        if (number.Length == 0)
        {
            return "Please enter a DTC number.";
        }

        if (number.Length > GroupLimits.MaxNumberLength)
        {
            return "The DTC number can have at most " + GroupLimits.MaxNumberLength + " digits.";
        }

        for (int i = 0; i < number.Length; i++)
        {
            if (number[i] < '0' || number[i] > '9')
            {
                return "The DTC number must be a whole number, like 4821.";
            }
        }

        // No leading zero, and not zero itself (so 7 and 07 can never be two different groups).
        if (number[0] == '0')
        {
            return "The DTC number must be a positive number without a leading zero.";
        }

        return null;
    }

    // Numbers must be unique across ALL groups, ignoring upper/lower case.
    private static string? CheckDuplicateNumber(List<AnnouncementGroup> existing, string number, string? excludeId)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            AnnouncementGroup other = existing[i];
            if (excludeId != null && other.Id == excludeId)
            {
                continue;
            }

            if (string.Equals(other.Number.Trim(), number, StringComparison.OrdinalIgnoreCase))
            {
                return "Another DTC already uses the number " + number + ".";
            }
        }

        return null;
    }

    // Names must be unique across ALL groups (active and inactive), ignoring upper/lower case.
    // excludeId is the group being edited, so it does not clash with itself.
    private static string? CheckDuplicateName(List<AnnouncementGroup> existing, string name, string? excludeId)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            AnnouncementGroup other = existing[i];
            if (excludeId != null && other.Id == excludeId)
            {
                continue;
            }

            if (string.Equals(other.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                return "A DTC named \"" + other.Name + "\" already exists.";
            }
        }

        return null;
    }

    private static AnnouncementGroup? FindById(List<AnnouncementGroup> groups, string id)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Id == id)
            {
                return groups[i];
            }
        }

        return null;
    }

    // Throws TimeoutException when the task takes longer than TimeoutSeconds.
    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
        if (finished != task)
        {
            throw new TimeoutException();
        }

        return await task;
    }

    private static async Task WithTimeoutAsync(Task task)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
        if (finished != task)
        {
            throw new TimeoutException();
        }

        await task;
    }

    private static string MapError(Exception ex, string fallbackMessage)
    {
        string text = (ex.GetType().FullName + " " + ex.Message).ToLowerInvariant();

        if (text.Contains("permission"))
        {
            return "You do not have permission to do that.";
        }

        return fallbackMessage;
    }

    private static int CompareByName(AnnouncementGroup a, AnnouncementGroup b)
    {
        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
    }
}
