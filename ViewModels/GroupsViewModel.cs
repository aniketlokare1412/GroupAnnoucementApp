using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class GroupsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IGroupAdminService _groupService;
    private readonly IDialogService _dialogs;

    private List<AnnouncementGroup> _allGroups = new List<AnnouncementGroup>();
    private bool _hasLoaded;

    public ObservableCollection<GroupListItem> Groups { get; } = new ObservableCollection<GroupListItem>();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private GroupListItem? _selectedGroup;

    [ObservableProperty]
    private bool _hasNoGroups;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public GroupsViewModel(
        INavigationService navigation,
        ISessionService session,
        IGroupAdminService groupService,
        IDialogService dialogs)
    {
        _navigation = navigation;
        _session = session;
        _groupService = groupService;
        _dialogs = dialogs;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Runs automatically when a row is tapped.
    partial void OnSelectedGroupChanged(GroupListItem? value)
    {
        if (value != null)
        {
            _ = ManageGroupAsync(value);
        }
    }

    // Called by the page every time it appears (so the list is fresh after adding or editing).
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            // Not an admin: never show this page.
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

    [RelayCommand]
    private Task AddGroupAsync()
    {
        return _navigation.GoToAsync(Routes.GroupEdit);
    }

    private async Task ManageGroupAsync(GroupListItem item)
    {
        try
        {
            if (IsBusy)
            {
                return;
            }

            string membersOption = "Manage members";
            string editOption = "Edit group";
            string activeOption = item.IsActive ? "Deactivate group" : "Activate group";

            string? choice = await _dialogs.ChooseAsync(item.Name, "Cancel", new string[] { membersOption, editOption, activeOption });
            if (choice == null)
            {
                return;
            }

            if (choice == membersOption)
            {
                string membersRoute = Routes.GroupMembers + "?groupId=" + Uri.EscapeDataString(item.Id);
                await _navigation.GoToAsync(membersRoute);
            }
            else if (choice == editOption)
            {
                string route = Routes.GroupEdit + "?groupId=" + Uri.EscapeDataString(item.Id);
                await _navigation.GoToAsync(route);
            }
            else if (choice == activeOption)
            {
                await ChangeActiveAsync(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("MANAGE GROUP ERROR: " + ex);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            // Clear the selection so the same row can be tapped again.
            SelectedGroup = null;
        }
    }

    private async Task ChangeActiveAsync(GroupListItem item)
    {
        bool newValue = !item.IsActive;

        string title;
        string message;
        string accept;
        if (newValue)
        {
            title = "Activate group";
            message = "Activate \"" + item.Name + "\"? It will count towards the limit of " + GroupLimits.MaxActiveGroups + " active groups.";
            accept = "Activate";
        }
        else
        {
            title = "Deactivate group";
            message = "Deactivate \"" + item.Name + "\"? Members will no longer see it. You can activate it again later.";
            accept = "Deactivate";
        }

        bool confirmed = await _dialogs.ConfirmAsync(title, message, accept, "Cancel");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        bool saved;
        try
        {
            OperationResult result = await _groupService.SetActiveAsync(item.Id, newValue);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                saved = false;
            }
            else
            {
                saved = true;
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (saved)
        {
            await LoadAsync();
        }
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Groups.Clear();
        int activeCount = 0;
        for (int i = 0; i < _allGroups.Count; i++)
        {
            AnnouncementGroup group = _allGroups[i];

            if (group.IsActive)
            {
                activeCount++;
            }

            if (Matches(group, term))
            {
                Groups.Add(GroupListItem.FromGroup(group));
            }
        }

        HasNoGroups = _hasLoaded && Groups.Count == 0;

        if (_hasLoaded)
        {
            SummaryText = Groups.Count + " of " + _allGroups.Count + " groups  |  "
                + activeCount + " of " + GroupLimits.MaxActiveGroups + " active";
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
