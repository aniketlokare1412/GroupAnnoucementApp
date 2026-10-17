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

    public CommunityService(
        ISessionService session,
        ICommunityRepository communityRepository,
        ISuperCommunityRepository superRepository)
    {
        _session = session;
        _communityRepository = communityRepository;
        _superRepository = superRepository;
    }

    public async Task<OperationResult<CommunityDirectory>> GetDirectoryAsync()
    {
        if (_session.CurrentProfile == null)
        {
            return OperationResult<CommunityDirectory>.Fail("You are signed out. Please sign in again.");
        }

        try
        {
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
