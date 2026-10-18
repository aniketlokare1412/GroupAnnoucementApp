using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.Services;

public class AudiencePickerService : IAudiencePickerService
{
    private readonly INavigationService _navigation;
    private TaskCompletionSource<HashSet<string>?>? _completion;

    public AudiencePickerService(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public AudienceRequest? Pending { get; private set; }

    public async Task<HashSet<string>?> PickAsync(List<AudienceGroup> groups, HashSet<string> selected)
    {
        // One picker at a time (a double tap must not open two).
        if (_completion != null)
        {
            return null;
        }

        TaskCompletionSource<HashSet<string>?> completion = new TaskCompletionSource<HashSet<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;

        AudienceRequest request = new AudienceRequest();
        request.Groups = groups;
        request.Selected = new HashSet<string>(selected);
        Pending = request;

        try
        {
            await _navigation.GoToAsync(Routes.AudiencePicker);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN AUDIENCE PICKER ERROR: " + ex);
            Complete(null);
        }

        return await completion.Task;
    }

    public void Complete(HashSet<string>? result)
    {
        TaskCompletionSource<HashSet<string>?>? completion = _completion;
        if (completion == null)
        {
            return;
        }

        _completion = null;
        Pending = null;
        completion.TrySetResult(result);
    }
}
