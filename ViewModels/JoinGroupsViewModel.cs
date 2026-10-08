using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Regular user: search the active groups and tap one to join it.
public partial class JoinGroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IMembershipService _membershipService;
    private readonly IDialogService _dialogs;

    private List<JoinableGroup> _allGroups = new List<JoinableGroup>();
    private bool _hasLoaded;

    public ObservableCollection<JoinGroupItem> Groups { get; } = new ObservableCollection<JoinGroupItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private JoinGroupItem? _selectedGroup;

    [ObservableProperty]
    private bool _hasNoGroups;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    // A soft warning (not an error): the list works, but something non-essential failed.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _noticeMessage;

    public bool HasNotice
    {
        get { return !string.IsNullOrWhiteSpace(NoticeMessage); }
    }

    public JoinGroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IMembershipService membershipService,
        IDialogService dialogs)
    {
        _navigation = navigation;
        _session = session;
        _membershipService = membershipService;
        _dialogs = dialogs;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedGroupChanged(JoinGroupItem? value)
    {
        if (value != null)
        {
            _ = JoinAsync(value);
        }
    }

    // Called by the page every time it appears.
    public async Task LoadAsync()
    {
        if (_session.CurrentProfile == null || _session.IsAdmin)
        {
            // Discover is a tab now (not a pushed page), so there is nothing to go back to.
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<BrowseGroupsResult> result = await _membershipService.GetBrowseGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            BrowseGroupsResult browse = result.Value ?? new BrowseGroupsResult();
            _allGroups = browse.Groups;

            if (browse.MembershipKnown)
            {
                NoticeMessage = null;
            }
            else
            {
                NoticeMessage = "We could not check which groups you have already joined. You can still search and join groups.";
            }

            _hasLoaded = true;
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    private async Task JoinAsync(JoinGroupItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            if (item.IsMember)
            {
                await _dialogs.AlertAsync("Already joined", "You are already a member of \"" + item.Name + "\".", "OK");
                return;
            }

            bool confirmed = await _dialogs.ConfirmAsync(
                "Join group",
                "Join \"" + item.Name + "\"?",
                "Join",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            bool joined;
            try
            {
                OperationResult result = await _membershipService.JoinGroupAsync(item.Id);
                if (!result.IsSuccess)
                {
                    ErrorMessage = result.ErrorMessage;
                    joined = false;
                }
                else
                {
                    joined = true;
                }
            }
            finally
            {
                IsBusy = false;
            }

            if (joined)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("JOIN GROUP ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedGroup = null;
        }
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Groups.Clear();
        for (int i = 0; i < _allGroups.Count; i++)
        {
            if (Matches(_allGroups[i].Group, term))
            {
                Groups.Add(JoinGroupItem.FromJoinable(_allGroups[i]));
            }
        }

        HasNoGroups = _hasLoaded && Groups.Count == 0;

        if (_hasLoaded)
        {
            SummaryText = Groups.Count + " of " + _allGroups.Count + " groups";
        }
        else
        {
            SummaryText = string.Empty;
        }
    }

    private static bool Matches(AnnouncementGroup group, string term)
    {
        if (term.Length == 0)
        {
            return true;
        }

        if (ContainsText(group.Name, term))
        {
            return true;
        }

        if (ContainsText(group.Description, term))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsText(string? text, string term)
    {
        if (text == null)
        {
            return false;
        }

        return text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
