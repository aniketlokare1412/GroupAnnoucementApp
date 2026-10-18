using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: read-only tree of the whole structure.
//   Super community -> its communities -> their groups
//   then: "Communities with no super community", "Groups with no community".
// A group in several communities appears under each of them. Tap a row to open its edit page,
// tap the arrow to expand or collapse. Searching by name or number shows the matches with
// their parents opened. The data comes from the same three reads as the Groups tab.
public partial class StructureViewModel : BaseViewModel
{
    private const string LooseCommunitiesKey = "section:communities";
    private const string LooseGroupsKey = "section:groups";

    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly ICommunityAdminService _communityService;

    private List<StructureNode> _roots = new List<StructureNode>();
    private HashSet<string> _expanded = new HashSet<string>();
    private bool _hasLoaded;

    // The rows that are visible right now.
    public ObservableCollection<StructureNode> Rows { get; } = new ObservableCollection<StructureNode>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private bool _hasNoRows;

    [ObservableProperty]
    private string _emptyTitle = string.Empty;

    [ObservableProperty]
    private string _emptyHint = string.Empty;

    [ObservableProperty]
    private bool _showLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    public StructureViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        ICommunityAdminService communityService)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _communityService = communityService;
    }

    partial void OnSearchTextChanged(string value)
    {
        Rebuild();
    }

    // Called by the page every time it appears, so edits made from here show up when coming back.
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            OperationResult<List<AnnouncementGroup>> groups = await _groupService.GetAllGroupsAsync();
            if (!groups.IsSuccess)
            {
                ErrorMessage = groups.ErrorMessage;
                return;
            }

            OperationResult<List<Community>> communities = await _communityService.GetAllCommunitiesAsync();
            if (!communities.IsSuccess)
            {
                ErrorMessage = communities.ErrorMessage;
                return;
            }

            OperationResult<List<SuperCommunity>> supers = await _communityService.GetAllSuperCommunitiesAsync();
            if (!supers.IsSuccess)
            {
                ErrorMessage = supers.ErrorMessage;
                return;
            }

            BuildTree(
                groups.Value ?? new List<AnnouncementGroup>(),
                communities.Value ?? new List<Community>(),
                supers.Value ?? new List<SuperCommunity>());

            _hasLoaded = true;
            Rebuild();
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        List<StructureNode> all = new List<StructureNode>();
        CollectAll(_roots, all);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].HasChildren)
            {
                _expanded.Add(all[i].Key);
            }
        }

        Rebuild();
    }

    [RelayCommand]
    private void CollapseAll()
    {
        _expanded.Clear();
        Rebuild();
    }

    // ---------- building the tree ----------

    private void BuildTree(List<AnnouncementGroup> groups, List<Community> communities, List<SuperCommunity> supers)
    {
        Dictionary<string, AnnouncementGroup> groupById = new Dictionary<string, AnnouncementGroup>();
        for (int i = 0; i < groups.Count; i++)
        {
            groupById[groups[i].Id] = groups[i];
        }

        HashSet<string> groupsInACommunity = new HashSet<string>();
        for (int i = 0; i < communities.Count; i++)
        {
            for (int j = 0; j < communities[i].GroupIds.Count; j++)
            {
                groupsInACommunity.Add(communities[i].GroupIds[j]);
            }
        }

        HashSet<string> communityIds = new HashSet<string>();
        for (int i = 0; i < communities.Count; i++)
        {
            communityIds.Add(communities[i].Id);
        }

        List<StructureNode> roots = new List<StructureNode>();
        bool firstLoad = !_hasLoaded;

        // Super communities, each with its communities.
        for (int i = 0; i < supers.Count; i++)
        {
            SuperCommunity super = supers[i];
            List<Community> inside = new List<Community>();
            for (int c = 0; c < communities.Count; c++)
            {
                if (communities[c].SuperCommunityId == super.Id)
                {
                    inside.Add(communities[c]);
                }
            }

            string key = "super:" + super.Id;
            StructureNode superNode = StructureNode.Create(
                StructureKind.Super, super.Id, key, super.Name, super.Number,
                CommunityText.Count(inside.Count, "community", "communities"), 0, false);
            Wire(superNode);

            for (int c = 0; c < inside.Count; c++)
            {
                superNode.Children.Add(BuildCommunityNode(inside[c], key, 1, groupById));
            }

            roots.Add(superNode);
        }

        // Communities with no super community (or whose super community no longer exists).
        List<Community> loose = new List<Community>();
        for (int c = 0; c < communities.Count; c++)
        {
            bool hasSuper = !string.IsNullOrEmpty(communities[c].SuperCommunityId) && ContainsSuper(supers, communities[c].SuperCommunityId);
            if (!hasSuper)
            {
                loose.Add(communities[c]);
            }
        }

        if (loose.Count > 0)
        {
            StructureNode section = StructureNode.Create(
                StructureKind.Section, string.Empty, LooseCommunitiesKey, "Communities with no super community", string.Empty,
                CommunityText.Count(loose.Count, "community", "communities"), 0, false);
            Wire(section);
            for (int c = 0; c < loose.Count; c++)
            {
                section.Children.Add(BuildCommunityNode(loose[c], LooseCommunitiesKey, 1, groupById));
            }

            roots.Add(section);
        }

        // Groups that are in no community.
        List<AnnouncementGroup> ungrouped = new List<AnnouncementGroup>();
        for (int i = 0; i < groups.Count; i++)
        {
            if (!groupsInACommunity.Contains(groups[i].Id))
            {
                ungrouped.Add(groups[i]);
            }
        }

        if (ungrouped.Count > 0)
        {
            StructureNode section = StructureNode.Create(
                StructureKind.Section, string.Empty, LooseGroupsKey, "Groups with no community", string.Empty,
                CommunityText.Count(ungrouped.Count, "group", "groups"), 0, false);
            Wire(section);
            for (int i = 0; i < ungrouped.Count; i++)
            {
                section.Children.Add(BuildGroupNode(ungrouped[i], LooseGroupsKey, 1));
            }

            roots.Add(section);
        }

        _roots = roots;

        // First time: open the top level so the communities are visible straight away.
        if (firstLoad)
        {
            _expanded = new HashSet<string>();
            for (int i = 0; i < _roots.Count; i++)
            {
                _expanded.Add(_roots[i].Key);
            }
        }
    }

    private StructureNode BuildCommunityNode(Community community, string parentKey, int depth, Dictionary<string, AnnouncementGroup> groupById)
    {
        string key = parentKey + "/community:" + community.Id;
        List<AnnouncementGroup> members = new List<AnnouncementGroup>();
        for (int i = 0; i < community.GroupIds.Count; i++)
        {
            AnnouncementGroup? group;
            if (groupById.TryGetValue(community.GroupIds[i], out group))
            {
                members.Add(group);
            }
        }

        members.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        StructureNode node = StructureNode.Create(
            StructureKind.Community, community.Id, key, community.Name, community.Number,
            CommunityText.Count(members.Count, "group", "groups"), depth, false);
        Wire(node);
        for (int i = 0; i < members.Count; i++)
        {
            node.Children.Add(BuildGroupNode(members[i], key, depth + 1));
        }

        return node;
    }

    private StructureNode BuildGroupNode(AnnouncementGroup group, string parentKey, int depth)
    {
        string caption = CommunityText.NumberText(group.Number);
        if (!group.IsActive)
        {
            caption = caption + "  -  Inactive";
        }

        StructureNode node = StructureNode.Create(
            StructureKind.Group, group.Id, parentKey + "/group:" + group.Id, group.Name, group.Number, caption, depth, !group.IsActive);
        node.IsUngrouped = parentKey == LooseGroupsKey;
        Wire(node);
        return node;
    }

    private static bool ContainsSuper(List<SuperCommunity> supers, string id)
    {
        for (int i = 0; i < supers.Count; i++)
        {
            if (supers[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private void Wire(StructureNode node)
    {
        node.OpenCommand = new AsyncRelayCommand(() => OpenAsync(node));
        node.ToggleCommand = new RelayCommand(() => Toggle(node));
    }

    // ---------- showing the tree ----------

    private void Toggle(StructureNode node)
    {
        if (!node.HasChildren)
        {
            return;
        }

        if (!_expanded.Remove(node.Key))
        {
            _expanded.Add(node.Key);
        }

        Rebuild();
    }

    // Tap on a row: its edit page. The section headers just open or close.
    private async Task OpenAsync(StructureNode node)
    {
        try
        {
            if (node.Kind == StructureKind.Section)
            {
                Toggle(node);
                return;
            }

            string route;
            if (node.Kind == StructureKind.Group)
            {
                route = Routes.GroupEdit + "?groupId=" + Uri.EscapeDataString(node.Id);
            }
            else
            {
                string kind = node.Kind == StructureKind.Super ? CommunityEditViewModel.KindSuper : CommunityEditViewModel.KindCommunity;
                route = Routes.CommunityEdit + "?kind=" + kind + "&communityId=" + Uri.EscapeDataString(node.Id);
            }

            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN STRUCTURE NODE ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    private void Rebuild()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Rows.Clear();
        int shown = 0;
        for (int i = 0; i < _roots.Count; i++)
        {
            shown += AddVisible(_roots[i], term, false);
        }

        HasNoRows = _hasLoaded && Rows.Count == 0;

        if (!_hasLoaded)
        {
            SummaryText = string.Empty;
            return;
        }

        if (term.Length > 0)
        {
            SummaryText = shown + " matching";
        }
        else
        {
            SummaryText = "Tap a row to edit it, tap the arrow to open or close it";
        }

        if (_roots.Count == 0)
        {
            EmptyTitle = "Nothing here yet";
            EmptyHint = "Create groups, communities and super communities in the Groups tab first.";
        }
        else
        {
            EmptyTitle = "No matches";
            EmptyHint = "Nothing matches \"" + term + "\". Try a different word.";
        }
    }

    // Adds the node and (when open) its children to Rows. Returns how many rows of the search matched.
    // With a search term a node is shown when it matches, when something below it matches, or when
    // something above it matches. Nodes that have a match below them are opened automatically.
    private int AddVisible(StructureNode node, string term, bool ancestorMatched)
    {
        bool searching = term.Length > 0;
        bool selfMatches = searching && Matches(node, term);
        bool descendantMatches = searching && HasMatchBelow(node, term);

        if (searching && !selfMatches && !descendantMatches && !ancestorMatched)
        {
            return 0;
        }

        // While searching, a node with a match below it opens by itself; the user can still open
        // any other node with its arrow (for example a matching community, to see all its groups).
        bool open = _expanded.Contains(node.Key) || descendantMatches;
        node.IsExpanded = open;
        Rows.Add(node);
        int matches = selfMatches ? 1 : 0;

        if (open)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                matches += AddVisible(node.Children[i], term, ancestorMatched || selfMatches);
            }
        }

        return matches;
    }

    private static bool HasMatchBelow(StructureNode node, string term)
    {
        for (int i = 0; i < node.Children.Count; i++)
        {
            if (Matches(node.Children[i], term) || HasMatchBelow(node.Children[i], term))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Matches(StructureNode node, string term)
    {
        if (node.Kind == StructureKind.Section)
        {
            return false;
        }

        return node.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
            || node.Number.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void CollectAll(List<StructureNode> nodes, List<StructureNode> into)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            into.Add(nodes[i]);
            CollectAll(nodes[i].Children, into);
        }
    }
}
