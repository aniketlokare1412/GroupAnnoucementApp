using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: plan an outage for one or more groups. The date, the from and to time, and when members
// should start seeing the message (now, or later).
public partial class OutageEditViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IOutageService _outageService;
    private readonly IGroupAdminService _groupService;
    private readonly IAudiencePickerService _audiencePicker;
    private readonly IToastService _toasts;

    private List<SelectableGroupItem> _allItems = new List<SelectableGroupItem>();
    private bool _isLoaded;

    public ObservableCollection<SelectableGroupItem> Groups { get; } = new ObservableCollection<SelectableGroupItem>();

    [ObservableProperty]
    private DateTime _outageDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private TimeSpan _fromTime = new TimeSpan(10, 0, 0);

    [ObservableProperty]
    private TimeSpan _toTime = new TimeSpan(17, 0, 0);

    // On: members see the message right away. Off: pick the date and time it appears.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPublishLater))]
    private bool _publishNow = true;

    [ObservableProperty]
    private DateTime _publishDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _publishTime = new TimeSpan(9, 0, 0);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCountText = "No groups selected";

    [ObservableProperty]
    private bool _showLoading;

    [ObservableProperty]
    private bool _hasNoGroups;

    public bool ShowPublishLater => !PublishNow;

    public OutageEditViewModel(
        INavigationService navigation,
        ISessionService session,
        IOutageService outageService,
        IGroupAdminService groupService,
        IAudiencePickerService audiencePicker,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _outageService = outageService;
        _groupService = groupService;
        _audiencePicker = audiencePicker;
        _toasts = toasts;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears.
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (_isLoaded || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ShowLoading = true;
        ErrorMessage = null;

        try
        {
            OperationResult<List<AnnouncementGroup>> result = await _groupService.GetAllGroupsAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _allItems = new List<SelectableGroupItem>();
            List<AnnouncementGroup> groups = result.Value ?? new List<AnnouncementGroup>();
            for (int i = 0; i < groups.Count; i++)
            {
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

    // Opens the full-screen picker: single groups, communities or super communities (search included).
    // Going back from the picker keeps the selection as it was.
    [RelayCommand]
    private async Task ChooseGroupsAsync()
    {
        if (IsBusy || !_isLoaded)
        {
            return;
        }

        List<AudienceGroup> groups = new List<AudienceGroup>();
        HashSet<string> selected = new HashSet<string>();
        for (int i = 0; i < _allItems.Count; i++)
        {
            AudienceGroup group = new AudienceGroup();
            group.Id = _allItems[i].Id;
            group.Name = _allItems[i].Name;
            group.Number = _allItems[i].Number;
            groups.Add(group);

            if (_allItems[i].IsSelected)
            {
                selected.Add(_allItems[i].Id);
            }
        }

        HashSet<string>? result = await _audiencePicker.PickAsync(groups, selected);
        if (result == null)
        {
            return;
        }

        for (int i = 0; i < _allItems.Count; i++)
        {
            _allItems[i].IsSelected = result.Contains(_allItems[i].Id);
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        List<SelectableGroupItem> chosen = new List<SelectableGroupItem>();
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (_allItems[i].IsSelected)
            {
                chosen.Add(_allItems[i]);
            }
        }

        if (chosen.Count == 0)
        {
            ErrorMessage = "Choose at least one group.";
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset start = new DateTimeOffset(OutageDate.Date + FromTime);
        DateTimeOffset end = new DateTimeOffset(OutageDate.Date + ToTime);
        DateTimeOffset publishAt = PublishNow ? now : new DateTimeOffset(PublishDate.Date + PublishTime);

        if (end <= start)
        {
            ErrorMessage = "The outage must end after it starts.";
            return;
        }

        if (start <= now)
        {
            ErrorMessage = "Choose a date and time in the future.";
            return;
        }

        if (publishAt > start)
        {
            ErrorMessage = "Members must see the message before the outage starts.";
            return;
        }

        IsBusy = true;
        bool leavePage = false;
        try
        {
            List<string> failed = new List<string>();
            for (int i = 0; i < chosen.Count; i++)
            {
                OperationResult result = await _outageService.ScheduleAsync(chosen[i].Id, start, end, publishAt);
                if (result.IsSuccess)
                {
                    chosen[i].IsSelected = false;
                }
                else
                {
                    failed.Add(chosen[i].Name + ": " + result.ErrorMessage);
                }
            }

            if (failed.Count > 0)
            {
                ErrorMessage = string.Join("\n", failed);
                return;
            }

            leavePage = true;
        }
        finally
        {
            IsBusy = false;
        }

        if (leavePage)
        {
            // Go back first, then show the toast (the toast waits for the next page).
            await _navigation.GoToAsync("..");
            _toasts.Show(chosen.Count == 1 ? "Outage scheduled" : "Outage scheduled for " + chosen.Count + " groups");
        }
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

        SelectedCountText = count == 0 ? "No groups selected" : (count == 1 ? "1 group selected" : count + " groups selected");
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();
        Groups.Clear();
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (term.Length == 0
                || _allItems[i].Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0
                || _allItems[i].Number.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Groups.Add(_allItems[i]);
            }
        }

        HasNoGroups = _isLoaded && Groups.Count == 0;
    }
}
