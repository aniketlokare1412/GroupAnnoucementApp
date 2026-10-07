using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: pick a group to see and manage ITS announcements.
public partial class AnnouncementGroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;

    private List<AnnouncementGroup> _allGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    public ObservableCollection<GroupListItem> Groups { get; } = new ObservableCollection<GroupListItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private GroupListItem? _selectedGroup;

    [ObservableProperty]
    private bool _hasNoGroups;

    public AnnouncementGroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedGroupChanged(GroupListItem? value)
    {
        if (value != null)
        {
            _ = OpenGroupAsync(value);
        }
    }

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

        try
        {
            OperationResult<List<AnnouncementGroup>> result = await _groupService.GetAllGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _allGroups = result.Value ?? new List<AnnouncementGroup>();
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

    private async Task OpenGroupAsync(GroupListItem item)
    {
        try
        {
            string route = Routes.Announcements
                + "?groupId=" + Uri.EscapeDataString(item.Id)
                + "&groupName=" + Uri.EscapeDataString(item.Name);
            await _navigation.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("OPEN GROUP ANNOUNCEMENTS ERROR: " + ex);
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
            AnnouncementGroup group = _allGroups[i];
            if (term.Length == 0 || group.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Groups.Add(GroupListItem.FromGroup(group));
            }
        }

        HasNoGroups = _hasLoaded && Groups.Count == 0;
    }
}
