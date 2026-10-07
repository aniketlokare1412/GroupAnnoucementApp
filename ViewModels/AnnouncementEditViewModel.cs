using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: one page, three modes (decided by the route):
//   announcementedit                                   -> new announcement, pick ONE OR MORE groups
//   announcementedit?groupId=..&groupName=..           -> new announcement for that one group
//   announcementedit?groupId=..&announcementId=..      -> edit an existing announcement
[QueryProperty(nameof(GroupId), "groupId")]
[QueryProperty(nameof(GroupName), "groupName")]
[QueryProperty(nameof(AnnouncementId), "announcementId")]
public partial class AnnouncementEditViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAnnouncementService _announcementService;
    private readonly IGroupAdminService _groupService;

    private List<SelectableGroupItem> _allItems = new List<SelectableGroupItem>();
    private bool _isLoaded;

    public ObservableCollection<SelectableGroupItem> Groups { get; } = new ObservableCollection<SelectableGroupItem>();

    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private string? _announcementId;

    [ObservableProperty]
    private string _titleText = "New announcement";

    [ObservableProperty]
    private string _primaryButtonText = "Publish";

    [ObservableProperty]
    private string _targetText = string.Empty;

    [ObservableProperty]
    private bool _hasTargetText;

    // True only in "pick one or more groups" mode.
    [ObservableProperty]
    private bool _isPickerVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCountText = "No groups selected";

    [ObservableProperty]
    private bool _hasNoGroups;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    public int MaxTitleLength
    {
        get { return AnnouncementLimits.MaxTitleLength; }
    }

    public int MaxMessageLength
    {
        get { return AnnouncementLimits.MaxMessageLength; }
    }

    private bool IsEditing
    {
        get { return !string.IsNullOrEmpty(AnnouncementId); }
    }

    private bool IsSingleGroup
    {
        get { return !string.IsNullOrEmpty(GroupId); }
    }

    public AnnouncementEditViewModel(
        INavigationService navigation,
        ISessionService session,
        IAnnouncementService announcementService,
        IGroupAdminService groupService)
    {
        _navigation = navigation;
        _session = session;
        _announcementService = announcementService;
        _groupService = groupService;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears. Loads once, so typed text and ticks are never lost.
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync("..");
            return;
        }

        if (_isLoaded || IsBusy)
        {
            return;
        }

        if (IsEditing)
        {
            await LoadForEditAsync();
        }
        else if (IsSingleGroup)
        {
            TitleText = "New announcement";
            PrimaryButtonText = "Publish";
            SetTarget("Posting to: " + GroupName);
            _isLoaded = true;
        }
        else
        {
            TitleText = "New announcement";
            PrimaryButtonText = "Publish";
            await LoadGroupPickerAsync();
        }
    }

    private async Task LoadForEditAsync()
    {
        if (string.IsNullOrEmpty(GroupId))
        {
            ErrorMessage = "Unknown group.";
            return;
        }

        TitleText = "Edit announcement";
        PrimaryButtonText = "Save";
        SetTarget("Group: " + GroupName);

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<Announcement?> result = await _announcementService.GetAsync(GroupId, AnnouncementId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the announcement.";
                return;
            }

            Title = result.Value.Title;
            Message = result.Value.Message;
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadGroupPickerAsync()
    {
        IsPickerVisible = true;
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

            for (int i = 0; i < _allItems.Count; i++)
            {
                _allItems[i].PropertyChanged -= OnItemPropertyChanged;
            }

            _allItems = new List<SelectableGroupItem>();
            List<AnnouncementGroup> groups = result.Value ?? new List<AnnouncementGroup>();
            for (int i = 0; i < groups.Count; i++)
            {
                // Announcements can only be posted to ACTIVE groups.
                if (groups[i].IsActive)
                {
                    SelectableGroupItem item = SelectableGroupItem.FromGroup(groups[i]);
                    item.PropertyChanged += OnItemPropertyChanged;
                    _allItems.Add(item);
                }
            }

            _isLoaded = true;
            ApplyFilter();
            UpdateSelectedCount();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        for (int i = 0; i < Groups.Count; i++)
        {
            Groups[i].IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        for (int i = 0; i < _allItems.Count; i++)
        {
            _allItems[i].IsSelected = false;
        }
    }

    // Does nothing until loading has finished, so a failed load can never be saved over.
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || !_isLoaded)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            if (IsEditing)
            {
                OperationResult updateResult = await _announcementService.UpdateAsync(GroupId!, AnnouncementId!, Title, Message);
                if (!updateResult.IsSuccess)
                {
                    ErrorMessage = updateResult.ErrorMessage;
                    return;
                }

                await _navigation.GoToAsync("..");
                return;
            }

            List<string> groupIds = new List<string>();
            if (IsSingleGroup)
            {
                groupIds.Add(GroupId!);
            }
            else
            {
                for (int i = 0; i < _allItems.Count; i++)
                {
                    if (_allItems[i].IsSelected)
                    {
                        groupIds.Add(_allItems[i].Id);
                    }
                }
            }

            if (groupIds.Count == 0)
            {
                ErrorMessage = "Select at least one group.";
                return;
            }

            OperationResult<PublishResult> result = await _announcementService.PublishAsync(Title, Message, groupIds);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            PublishResult published = result.Value ?? new PublishResult();
            if (published.FailedGroupIds.Count == 0)
            {
                await _navigation.GoToAsync("..");
                return;
            }

            ShowPartialFailure(published, groupIds.Count);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigation.GoToAsync("..");
    }

    // Some groups got the announcement and some did not. Remove the groups that already
    // have it, so tapping Publish again only retries the failed ones (no duplicates).
    private void ShowPartialFailure(PublishResult published, int totalCount)
    {
        List<string> failedNames = new List<string>();
        List<SelectableGroupItem> kept = new List<SelectableGroupItem>();

        for (int i = 0; i < _allItems.Count; i++)
        {
            SelectableGroupItem item = _allItems[i];

            if (published.PostedGroupIds.Contains(item.Id))
            {
                item.PropertyChanged -= OnItemPropertyChanged;
                continue;
            }

            if (published.FailedGroupIds.Contains(item.Id))
            {
                failedNames.Add(item.Name);
            }

            kept.Add(item);
        }

        _allItems = kept;
        ApplyFilter();
        UpdateSelectedCount();

        string text = "Posted to " + published.PostedGroupIds.Count + " of " + totalCount + " groups. "
            + "Not posted to: " + string.Join(", ", failedNames) + ". ";
        if (!string.IsNullOrEmpty(published.ErrorMessage))
        {
            text = text + published.ErrorMessage + " ";
        }

        text = text + "Tap Publish to try the remaining groups again.";
        ErrorMessage = text;
    }

    private void SetTarget(string text)
    {
        TargetText = text;
        HasTargetText = true;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableGroupItem.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        int count = 0;
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (_allItems[i].IsSelected)
            {
                count++;
            }
        }

        if (count == 0)
        {
            SelectedCountText = "No groups selected";
        }
        else if (count == 1)
        {
            SelectedCountText = "1 group selected";
        }
        else
        {
            SelectedCountText = count + " groups selected";
        }
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Groups.Clear();
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (term.Length == 0 || _allItems[i].Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Groups.Add(_allItems[i]);
            }
        }

        HasNoGroups = _isLoaded && IsPickerVisible && Groups.Count == 0;
    }
}
