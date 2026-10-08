using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: the announcement composer. One page, three modes (decided by the route):
//   announcementedit                                   -> new announcement, pick ONE OR MORE groups
//   announcementedit?groupId=..&groupName=..           -> new announcement for that one group
//   announcementedit?groupId=..&groupName=..&announcementId=..  -> edit an existing announcement
// Cancel is the back arrow. After a successful save the page goes back and a toast confirms it.
[QueryProperty(nameof(GroupId), "groupId")]
[QueryProperty(nameof(GroupName), "groupName")]
[QueryProperty(nameof(AnnouncementId), "announcementId")]
public partial class AnnouncementEditViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAnnouncementService _announcementService;
    private readonly IGroupAdminService _groupService;
    private readonly IToastService _toasts;

    private List<SelectableGroupItem> _allItems = new List<SelectableGroupItem>();
    private bool _isLoaded;
    private bool _isSaving;

    // The cards of the "Post to" picker (filtered by the search box).
    public ObservableCollection<SelectableGroupItem> Groups { get; } = new ObservableCollection<SelectableGroupItem>();

    // Set by Shell from the route.
    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private string? _announcementId;

    // The page title ("New announcement" / "Edit announcement").
    [ObservableProperty]
    private string _titleText = "New announcement";

    // The one main button at the bottom of the page.
    [ObservableProperty]
    private string _primaryButtonText = "Publish";

    // The card that says where the announcement goes (single group and edit modes).
    [ObservableProperty]
    private bool _hasTarget;

    [ObservableProperty]
    private string _targetCaption = string.Empty;

    [ObservableProperty]
    private string _targetName = string.Empty;

    [ObservableProperty]
    private string _targetInitials = string.Empty;

    // True only in "pick one or more groups" mode.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPickerSkeleton))]
    [NotifyPropertyChangedFor(nameof(ShowEditSpinner))]
    private bool _isPickerVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCountText = "No groups selected";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _hasNoGroups;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    // "12/100" and "340/2000", shown next to the field labels.
    [ObservableProperty]
    private string _titleCountText = string.Empty;

    [ObservableProperty]
    private string _messageCountText = string.Empty;

    // True while the picker or the announcement is being loaded.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPickerSkeleton))]
    [NotifyPropertyChangedFor(nameof(ShowEditSpinner))]
    private bool _showLoading;

    // True when loading failed, so the page can offer "Try again".
    [ObservableProperty]
    private bool _showRetry;

    // A warning (not an error): used when only some of the groups received the announcement.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _noticeMessage;

    public bool HasNoSelection => !HasSelection;

    // Loading the group list: the pill area shows grey placeholder pills.
    public bool ShowPickerSkeleton => ShowLoading && IsPickerVisible;

    // Loading the post being edited: a small spinner over the message box.
    public bool ShowEditSpinner => ShowLoading && !IsPickerVisible;

    public bool HasNotice => !string.IsNullOrWhiteSpace(NoticeMessage);

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
        IGroupAdminService groupService,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _announcementService = announcementService;
        _groupService = groupService;
        _toasts = toasts;

        TitleCountText = BuildCountText(string.Empty, AnnouncementLimits.MaxTitleLength);
        MessageCountText = BuildCountText(string.Empty, AnnouncementLimits.MaxMessageLength);
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnTitleChanged(string value)
    {
        TitleCountText = BuildCountText(value, AnnouncementLimits.MaxTitleLength);
    }

    partial void OnMessageChanged(string value)
    {
        MessageCountText = BuildCountText(value, AnnouncementLimits.MaxMessageLength);
    }

    // Called by the page every time it appears. Loads once, so typed text and ticks are never lost.
    // If loading failed, the next appearance (or "Try again") loads again.
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
            SetTarget("Posting to", GroupName);
            RefreshButtonText();
            _isLoaded = true;
        }
        else
        {
            TitleText = "New announcement";
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
        SetTarget("Editing in", GroupName);
        RefreshButtonText();

        IsBusy = true;
        ShowLoading = true;
        ShowRetry = false;
        ErrorMessage = null;

        try
        {
            OperationResult<Announcement?> result = await _announcementService.GetAsync(GroupId, AnnouncementId!);
            if (!result.IsSuccess || result.Value == null)
            {
                ErrorMessage = result.ErrorMessage ?? "We could not load the announcement.";
                ShowRetry = true;
                return;
            }

            Title = result.Value.Title;
            Message = result.Value.Message;
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
            ShowLoading = false;
        }
    }

    private async Task LoadGroupPickerAsync()
    {
        IsPickerVisible = true;
        IsBusy = true;
        ShowLoading = true;
        ShowRetry = false;
        ErrorMessage = null;

        try
        {
            OperationResult<List<AnnouncementGroup>> result = await _groupService.GetAllGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                ShowRetry = true;
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
            ShowLoading = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        return LoadAsync();
    }

    // Ticks every group that is currently shown (so a search narrows what "Select all" does).
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
        _isSaving = true;
        RefreshButtonText();
        ErrorMessage = null;
        NoticeMessage = null;

        // Decided inside the try block, acted on after it (so the button is free again first).
        bool leavePage = false;
        string? toastText = null;

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

                leavePage = true;
                toastText = "Changes saved";
            }
            else
            {
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
                    leavePage = true;
                    toastText = groupIds.Count == 1 ? "Announcement posted" : "Posted to " + groupIds.Count + " groups";
                }
                else
                {
                    ShowPartialFailure(published, groupIds.Count);
                }
            }
        }
        finally
        {
            _isSaving = false;
            IsBusy = false;
            RefreshButtonText();
        }

        if (leavePage)
        {
            // Go back first, then show the toast: the page we return to picks it up.
            await _navigation.GoToAsync("..");
            if (toastText != null)
            {
                _toasts.Show(toastText);
            }
        }
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
        NoticeMessage = text;
    }

    private void SetTarget(string caption, string? name)
    {
        TargetCaption = caption;
        TargetName = string.IsNullOrWhiteSpace(name) ? "This group" : name;
        TargetInitials = InitialsHelper.From(name);
        HasTarget = true;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableGroupItem.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private int CountSelected()
    {
        int count = 0;
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (_allItems[i].IsSelected)
            {
                count++;
            }
        }

        return count;
    }

    private void UpdateSelectedCount()
    {
        int count = CountSelected();
        HasSelection = count > 0;

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

        RefreshButtonText();
    }

    // The main button says what it will do: "Publish", "Publish to 3 groups", "Save changes".
    private void RefreshButtonText()
    {
        if (_isSaving)
        {
            PrimaryButtonText = IsEditing ? "Saving..." : "Publishing...";
            return;
        }

        if (IsEditing)
        {
            PrimaryButtonText = "Save changes";
            return;
        }

        if (IsPickerVisible)
        {
            int count = CountSelected();
            if (count == 0)
            {
                PrimaryButtonText = "Publish";
            }
            else if (count == 1)
            {
                PrimaryButtonText = "Publish to 1 group";
            }
            else
            {
                PrimaryButtonText = "Publish to " + count + " groups";
            }

            return;
        }

        PrimaryButtonText = "Publish";
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

    private static string BuildCountText(string? text, int max)
    {
        int length = text == null ? 0 : text.Length;
        return length + "/" + max;
    }
}
