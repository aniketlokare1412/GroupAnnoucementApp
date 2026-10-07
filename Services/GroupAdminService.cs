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

    public GroupAdminService(ISessionService session, IAuthService authService, IGroupRepository groupRepository)
    {
        _session = session;
        _authService = authService;
        _groupRepository = groupRepository;
    }

    public async Task<OperationResult<List<AnnouncementGroup>>> GetAllGroupsAsync()
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("You do not have permission to view groups.");
        }

        try
        {
            List<AnnouncementGroup> groups = await WithTimeoutAsync(_groupRepository.GetAllAsync());
            groups.Sort(CompareByName);
            return OperationResult<List<AnnouncementGroup>>.Success(groups);
        }
        catch (TimeoutException)
        {
            return OperationResult<List<AnnouncementGroup>>.Fail("Loading groups is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD GROUPS ERROR: " + ex);
            return OperationResult<List<AnnouncementGroup>>.Fail(MapError(ex, "We could not load the groups. Please check your connection and try again."));
        }
    }

    public async Task<OperationResult<AnnouncementGroup?>> GetGroupAsync(string id)
    {
        if (!_session.IsAdmin)
        {
            return OperationResult<AnnouncementGroup?>.Fail("You do not have permission to view groups.");
        }

        if (string.IsNullOrEmpty(id))
        {
            return OperationResult<AnnouncementGroup?>.Fail("Unknown group.");
        }

        try
        {
            AnnouncementGroup? group = await WithTimeoutAsync(_groupRepository.GetByIdAsync(id));
            if (group == null)
            {
                return OperationResult<AnnouncementGroup?>.Fail("This group no longer exists.");
            }

            return OperationResult<AnnouncementGroup?>.Success(group);
        }
        catch (TimeoutException)
        {
            return OperationResult<AnnouncementGroup?>.Fail("Loading the group is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD GROUP ERROR: " + ex);
            return OperationResult<AnnouncementGroup?>.Fail(MapError(ex, "We could not load the group. Please try again."));
        }
    }

    public async Task<OperationResult> CreateGroupAsync(string name, string description)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();

        string? problem = CheckCanModify();
        if (problem == null)
        {
            problem = ValidateText(cleanName, cleanDescription);
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
            // Read the current groups first: needed for the duplicate and limit checks.
            List<AnnouncementGroup> existing = await WithTimeoutAsync(_groupRepository.GetAllAsync());

            problem = CheckDuplicateName(existing, cleanName, null);
            if (problem == null)
            {
                problem = CheckLimit(existing);
            }
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            AnnouncementGroup group = new AnnouncementGroup();
            group.Name = cleanName;
            group.Description = cleanDescription;
            group.IsActive = true;
            group.CreatedBy = uid;

            string id = Guid.NewGuid().ToString("N");
            await WithTimeoutAsync(_groupRepository.CreateAsync(id, group));
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the groups list to see whether the group was created.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CREATE GROUP ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not create the group. Please try again."));
        }
    }

    public async Task<OperationResult> UpdateGroupAsync(string id, string name, string description)
    {
        string cleanName = (name ?? string.Empty).Trim();
        string cleanDescription = (description ?? string.Empty).Trim();

        string? problem = CheckCanModify();
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown group.";
        }
        if (problem == null)
        {
            problem = ValidateText(cleanName, cleanDescription);
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
                return OperationResult.Fail("This group no longer exists.");
            }

            problem = CheckDuplicateName(existing, cleanName, id);
            if (problem != null)
            {
                return OperationResult.Fail(problem);
            }

            await WithTimeoutAsync(_groupRepository.UpdateDetailsAsync(id, cleanName, cleanDescription));
            return OperationResult.Success();
        }
        catch (TimeoutException)
        {
            return OperationResult.Fail("The request is taking too long. Check the groups list to see the current details.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("UPDATE GROUP ERROR: " + ex);
            return OperationResult.Fail(MapError(ex, "We could not save the group. Please try again."));
        }
    }

    public async Task<OperationResult> SetActiveAsync(string id, bool isActive)
    {
        string? problem = CheckCanModify();
        if (problem == null && string.IsNullOrEmpty(id))
        {
            problem = "Unknown group.";
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
                return OperationResult.Fail("This group no longer exists.");
            }

            // Only activating a currently inactive group can push us over the limit.
            if (isActive && !group.IsActive)
            {
                problem = CheckLimit(existing);
                if (problem != null)
                {
                    return OperationResult.Fail(problem);
                }
            }

            await WithTimeoutAsync(_groupRepository.SetActiveAsync(id, isActive));
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
            return "You do not have permission to change groups.";
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
            return "Please enter a group name.";
        }

        if (name.Length > GroupLimits.MaxNameLength)
        {
            return "The group name can be at most " + GroupLimits.MaxNameLength + " characters.";
        }

        if (description.Length > GroupLimits.MaxDescriptionLength)
        {
            return "The description can be at most " + GroupLimits.MaxDescriptionLength + " characters.";
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
                return "A group named \"" + other.Name + "\" already exists.";
            }
        }

        return null;
    }

    private static string? CheckLimit(List<AnnouncementGroup> existing)
    {
        int active = 0;
        for (int i = 0; i < existing.Count; i++)
        {
            if (existing[i].IsActive)
            {
                active++;
            }
        }

        if (active >= GroupLimits.MaxActiveGroups)
        {
            return "You already have " + GroupLimits.MaxActiveGroups + " active groups, which is the maximum. Deactivate a group first.";
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
