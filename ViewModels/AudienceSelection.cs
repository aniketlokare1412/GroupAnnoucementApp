using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// The audience an admin has chosen for a new announcement or outage, kept on the form itself.
// The form shows it as "N groups selected" and two pills (Groups / Communities). Tapping a pill (or the card)
// opens the full-screen picker on that tab and, when the admin taps Done, the choice comes back here.
// Going back from the picker changes nothing. Used by AnnouncementEditViewModel and OutageEditViewModel.
public sealed class AudienceSelection
{
    private readonly IGroupAdminService _groupService;
    private readonly IAudiencePickerService _picker;
    private List<AudienceGroup>? _active;

    public AudienceSelection(IGroupAdminService groupService, IAudiencePickerService picker)
    {
        _groupService = groupService;
        _picker = picker;
    }

    // The chosen group ids (empty until the admin has chosen).
    public HashSet<string> GroupIds { get; private set; } = new HashSet<string>();

    // Which tab the choice was made in: 0 = Groups, 1 = Communities.
    public int Kind { get; private set; }

    // What the Communities tab had chosen (null for a Groups choice).
    public AudienceHierarchyState? Hierarchy { get; private set; }

    public int Count
    {
        get { return GroupIds.Count; }
    }

    public string CountText
    {
        get
        {
            if (Count == 0)
            {
                return "No DTCs selected";
            }

            return Count == 1 ? "1 DTC selected" : Count + " DTCs selected";
        }
    }

    public string CaptionText
    {
        get
        {
            if (Count == 0)
            {
                return "Choose DTCs or AB Switches below";
            }

            return Kind == 1 ? "From AB Switches - tap to change" : "From DTCs - tap to change";
        }
    }

    public string NameOf(string groupId)
    {
        if (_active != null)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id == groupId)
                {
                    return _active[i].Name;
                }
            }
        }

        return groupId;
    }

    // After a partly failed send: the groups that already got it are dropped, so Publish again only retries the rest.
    public void RemoveGroups(IEnumerable<string> groupIds)
    {
        foreach (string id in groupIds)
        {
            GroupIds.Remove(id);
        }

        // What is left is a plain list of groups now (it no longer matches the communities it came from).
        Kind = 0;
        Hierarchy = null;
    }

    // Opens the picker on the given tab. Returns an error message to show, or null (also when the admin just went back).
    public async Task<string?> ChooseAsync(int kind)
    {
        if (_active == null)
        {
            OperationResult<List<AnnouncementGroup>> loaded = await _groupService.GetAllGroupsAsync();
            if (!loaded.IsSuccess)
            {
                return loaded.ErrorMessage ?? "We could not load the DTCs.";
            }

            List<AudienceGroup> groups = new List<AudienceGroup>();
            List<AnnouncementGroup> all = loaded.Value ?? new List<AnnouncementGroup>();
            for (int i = 0; i < all.Count; i++)
            {
                // Messages can only go to ACTIVE groups.
                if (all[i].IsActive)
                {
                    AudienceGroup group = new AudienceGroup();
                    group.Id = all[i].Id;
                    group.Name = all[i].Name;
                    group.Number = all[i].Number;
                    groups.Add(group);
                }
            }

            _active = groups;
        }

        if (_active.Count == 0)
        {
            return "Create or activate a DTC first.";
        }

        // Reopening the tab the choice came from shows it again. The other tab starts empty.
        bool sameTab = kind == Kind && GroupIds.Count > 0;
        HashSet<string> selected = sameTab && kind == 0 ? new HashSet<string>(GroupIds) : new HashSet<string>();
        AudienceHierarchyState? hierarchy = sameTab && kind == 1 ? Hierarchy : null;

        HashSet<string>? result = await _picker.PickAsync(_active, selected, kind, hierarchy);
        if (result == null || result.Count == 0)
        {
            return null;
        }

        GroupIds = new HashSet<string>(result);
        Kind = kind;
        Hierarchy = kind == 1 ? _picker.LastHierarchy : null;
        return null;
    }
}
