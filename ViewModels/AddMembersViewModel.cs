using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: tick several eligible users and add them to the group (groupId comes from the route).
[QueryProperty(nameof(GroupId), "groupId")]
public partial class AddMembersViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IMembershipAdminService _membershipService;
    private readonly IToastService _toasts;

    private List<SelectableUserItem> _allItems = new List<SelectableUserItem>();
    private bool _isLoaded;

    public ObservableCollection<SelectableUserItem> Users { get; } = new ObservableCollection<SelectableUserItem>();

    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCountText = "No users selected";

    // Text of the main button: "Add selected" or "Add 3 members".
    [ObservableProperty]
    private string _addButtonText = "Add selected";

    // True when at least one user is ticked (the count chip turns teal).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private bool _hasSelection;

    public bool HasNoSelection
    {
        get { return !HasSelection; }
    }

    [ObservableProperty]
    private bool _hasNoUsers;

    public AddMembersViewModel(
        INavigationService navigation,
        ISessionService session,
        IMembershipAdminService membershipService,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _membershipService = membershipService;
        _toasts = toasts;
    }

    // Runs automatically whenever the search text changes.
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    // Called by the page every time it appears. The list is loaded once so ticks are never lost.
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

        if (string.IsNullOrEmpty(GroupId))
        {
            ErrorMessage = "Unknown group.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            OperationResult<List<UserProfile>> result = await _membershipService.GetEligibleUsersAsync(GroupId);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            for (int i = 0; i < _allItems.Count; i++)
            {
                _allItems[i].PropertyChanged -= OnItemPropertyChanged;
            }

            _allItems = new List<SelectableUserItem>();
            List<UserProfile> profiles = result.Value ?? new List<UserProfile>();
            for (int i = 0; i < profiles.Count; i++)
            {
                SelectableUserItem item = SelectableUserItem.FromProfile(profiles[i]);
                item.PropertyChanged += OnItemPropertyChanged;
                _allItems.Add(item);
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
    private async Task AddSelectedAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(GroupId))
        {
            return;
        }

        List<string> selectedIds = new List<string>();
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (_allItems[i].IsSelected)
            {
                selectedIds.Add(_allItems[i].Id);
            }
        }

        if (selectedIds.Count == 0)
        {
            ErrorMessage = "Select at least one user.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        bool succeeded;
        string? failMessage = null;
        int addedCount = selectedIds.Count;
        try
        {
            OperationResult<int> result = await _membershipService.AddMembersAsync(GroupId, selectedIds);
            succeeded = result.IsSuccess;
            failMessage = result.ErrorMessage;
            if (succeeded && result.Value > 0)
            {
                addedCount = result.Value;
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (succeeded)
        {
            await _navigation.GoToAsync("..");

            // After going back: the Members page shows it (or keeps it until it appears).
            _toasts.Show(addedCount == 1 ? "Added 1 member" : "Added " + addedCount + " members");
            return;
        }

        // Some users may have been added before the failure, so reload the eligible list
        // (they disappear from it) and then show the message.
        _isLoaded = false;
        await LoadAsync();
        ErrorMessage = failMessage;
    }

    [RelayCommand]
    private Task CancelAsync()
    {
        return _navigation.GoToAsync("..");
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableUserItem.IsSelected))
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

        HasSelection = count > 0;

        if (count == 0)
        {
            SelectedCountText = "No users selected";
            AddButtonText = "Add selected";
        }
        else
        {
            SelectedCountText = count + " selected";
            AddButtonText = count == 1 ? "Add 1 member" : "Add " + count + " members";
        }
    }

    private void ApplyFilter()
    {
        string term = (SearchText ?? string.Empty).Trim();

        Users.Clear();
        for (int i = 0; i < _allItems.Count; i++)
        {
            if (Matches(_allItems[i], term))
            {
                Users.Add(_allItems[i]);
            }
        }

        HasNoUsers = _isLoaded && Users.Count == 0;
    }

    private static bool Matches(SelectableUserItem item, string term)
    {
        if (term.Length == 0)
        {
            return true;
        }

        if (ContainsText(item.Name, term))
        {
            return true;
        }

        if (ContainsText(item.Email, term))
        {
            return true;
        }

        if (ContainsText(item.Phone, term))
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
