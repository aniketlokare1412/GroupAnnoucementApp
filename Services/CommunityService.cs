using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Repositories.Interfaces;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class CommunityService : ICommunityService
{
    private readonly ISessionService _session;
    private readonly ICommunityRepository _communityRepository;
    private readonly ISuperCommunityRepository _superRepository;
    private readonly IDirectoryService _directoryService;

    // Home's refresh runs every 20 seconds. The structure rarely changes, so a copy up to 60 seconds old is fine
    // (the screens also keep their own longer cache for the headings).
    private const int DirectoryMaxAgeSeconds = 60;

    public CommunityService(
        ISessionService session,
        ICommunityRepository communityRepository,
        ISuperCommunityRepository superRepository,
        IDirectoryService directoryService)
    {
        _session = session;
        _communityRepository = communityRepository;
        _superRepository = superRepository;
        _directoryService = directoryService;
    }

    public async Task<OperationResult<CommunityDirectory>> GetDirectoryAsync()
    {
        if (_session.CurrentProfile == null)
        {
            return OperationResult<CommunityDirectory>.Fail("You are signed out. Please sign in again.");
        }

        try
        {
            // Phase 1: one read of the directory document instead of two collection reads.
            // Falls back to the collections while the directory has not been built.
            DirectorySnapshot? snapshot = await _directoryService.GetAsync(DirectoryMaxAgeSeconds);
            if (snapshot != null)
            {
                CommunityDirectory fromDirectory = new CommunityDirectory();
                fromDirectory.Communities = snapshot.CopyCommunities();
                fromDirectory.SuperCommunities = snapshot.CopySuperCommunities();
                return OperationResult<CommunityDirectory>.Success(fromDirectory);
            }

            Task<List<Community>> communitiesTask = _communityRepository.GetAllAsync();
            Task<List<SuperCommunity>> supersTask = _superRepository.GetAllAsync();
            await ServiceHelper.WithTimeoutAsync(Task.WhenAll(communitiesTask, supersTask));

            CommunityDirectory directory = new CommunityDirectory();
            directory.Communities = communitiesTask.Result;
            directory.SuperCommunities = supersTask.Result;
            directory.Communities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            directory.SuperCommunities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return OperationResult<CommunityDirectory>.Success(directory);
        }
        catch (TimeoutException)
        {
            return OperationResult<CommunityDirectory>.Fail("Loading communities is taking too long. Please check your connection and try again.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("LOAD COMMUNITIES ERROR: " + ex);
            return OperationResult<CommunityDirectory>.Fail(ServiceHelper.MapError(ex, "We could not load the communities. Please check your connection and try again."));
        }
    }
}
