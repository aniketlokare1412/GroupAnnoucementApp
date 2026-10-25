using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// The announcements of ONE group, newest first, shown AnnouncementLimits.PageSize at a time.
// Admins also see deleted ones and get New post / edit / delete icons. Members get a leave icon.
[QueryProperty(nameof(GroupId), "groupId")]
[QueryProperty(nameof(GroupName), "groupName")]
public partial class AnnouncementsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAuthService _authService;
    private readonly IAnnouncementService _announcementService;
    private readonly IAttachmentService _attachments;
    private readonly IPanicService _panicService;
    private readonly IOutageService _outageService;
    private readonly IMembershipService _membershipService;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private bool _hasLoaded;

    // Everything that was loaded, newest first. The list on screen shows the first _shownCount.
    private List<Announcement> _loaded = new List<Announcement>();
    private int _shownCount;

    public ObservableCollection<AnnouncementListItem> Items { get; } = new ObservableCollection<AnnouncementListItem>();

    // Set by Shell from the route (announcements?groupId=...&groupName=...).
    [ObservableProperty]
    private string? _groupId;

    [ObservableProperty]
    private string? _groupName;

    [ObservableProperty]
    private bool _isAdminView;

    [ObservableProperty]
    private bool _isMemberView;

    [ObservableProperty]
    private bool _hasNoItems;

    [ObservableProperty]
    private bool _hasMore;

    // Header: the group's initials and "3 announcements".
    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private string _countText = string.Empty;

    // Empty state text (different for members and admins).
    [ObservableProperty]
    private string _emptyHint = string.Empty;

    // True only for the very first load, so a refresh or a delete never blanks the screen.
    [ObservableProperty]
    private bool _showLoading;

    // Bound to the pull-to-refresh control.
    [ObservableProperty]
    private bool _isRefreshing;

    private readonly IAudiencePickerService _audiencePicker;

    public AnnouncementsViewModel(
        INavigationService navigation,
        ISessionService session,
        IAuthService authService,
        IAnnouncementService announcementService,
        IMembershipService membershipService,
        IDialogService dialogs,
        IToastService toasts,
        IAttachmentService attachments,
        IPanicService panicService,
        IOutageService outageService,
        IAudiencePickerService audiencePicker)
    {
        _audiencePicker = audiencePicker;
        _outageService = outageService;
        _panicService = panicService;
        _attachments = attachments;
        _navigation = navigation;
        _session = session;
        _authService = authService;
        _announcementService = announcementService;
        _membershipService = membershipService;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Called by the page every time it appears (so the list is fresh after posting or editing).
    public async Task LoadAsync()
    {
        using (PerfTimer.Start("Announcements.Load"))
        {
            await LoadUntimedAsync();
        }
    }

    private async Task LoadUntimedAsync()
    {
        if (_session.CurrentProfile == null)
        {
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrEmpty(GroupId))
        {
            ErrorMessage = "Unknown group.";
            return;
        }

        IsAdminView = _session.IsAdmin;
        IsMemberView = !_session.IsAdmin;

        Initials = InitialsHelper.From(GroupName);
        EmptyHint = IsAdminView
            ? "Tap New post to write the first one."
            : "When an admin posts here, it will show up on this page.";

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            await LoadOutageAsync();

            OperationResult<List<Announcement>> result = await _announcementService.GetForGroupAsync(GroupId);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _loaded = result.Value ?? new List<Announcement>();
            _hasLoaded = true;
            _shownCount = 0;
            Items.Clear();
            ShowNextPage();
            CountText = BuildCountText();
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

    // Deleted announcements (admins only) are not counted.
    private string BuildCountText()
    {
        int active = 0;
        for (int i = 0; i < _loaded.Count; i++)
        {
            if (_loaded[i].IsActive)
            {
                active++;
            }
        }

        if (active == 0)
        {
            return "No announcements yet";
        }

        return active == 1 ? "1 announcement" : active + " announcements";
    }

    // Shows the next batch of already-loaded announcements (no network call).
    // Admin: the planned outage of this group (or its cancellation) is shown above the announcements.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutage))]
    private OutageCardItem? _outage;

    public bool HasOutage => Outage != null;

    private async Task LoadOutageAsync()
    {
        if (!IsAdminView || string.IsNullOrEmpty(GroupId))
        {
            Outage = null;
            return;
        }

        OperationResult<Outage?> result = await _outageService.GetAsync(GroupId);
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (result.IsSuccess && result.Value != null && result.Value.EndMs > nowMs)
        {
            Outage = OutageCardItem.From(result.Value, CancelOutageAsync);
        }
        else
        {
            Outage = null;
        }
    }

    private async Task CancelOutageAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(GroupId))
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Cancel outage",
            "Cancel this planned outage? Members will see an outage-cancelled message instead.",
            "Cancel outage",
            "Keep it");
        if (!confirmed)
        {
            return;
        }

        OperationResult result = await _outageService.CancelAsync(GroupId);
        if (!result.IsSuccess)
        {
            _toasts.Show(result.ErrorMessage ?? "We could not cancel the outage.");
            return;
        }

        _toasts.Show("Outage cancelled");
        await LoadOutageAsync();
    }

    [RelayCommand]
    private void LoadMore()
    {
        ShowNextPage();
    }

    private void ShowNextPage()
    {
        string? uid = _authService.GetCurrentUserId();

        int target = _shownCount + AnnouncementLimits.PageSize;
        if (target > _loaded.Count)
        {
            target = _loaded.Count;
        }

        for (int i = _shownCount; i < target; i++)
        {
            AnnouncementListItem item = AnnouncementListItem.FromAnnouncement(_loaded[i], uid, EditItemAsync, DeleteItemAsync, IsAdminView, _attachments);
            item.Extras = new AnnouncementExtras(_loaded[i].GroupId, _loaded[i], IsAdminView, _announcementService, _panicService, _dialogs, _toasts);
            Items.Add(item);
            _ = item.LoadImageAsync(_attachments);
            _ = item.Extras.LoadAsync();
        }

        _shownCount = target;
        HasMore = _shownCount < _loaded.Count;
        HasNoItems = _loaded.Count == 0;
    }

    // Admin: a new announcement for THIS group only.
    [RelayCommand]
    private Task NewAsync()
    {
        // Even from one group's page the admin first chooses the audience (here: one group), then writes the post.
        return _audiencePicker.StartAsync(Routes.AnnouncementEdit);
    }

    private Task EditItemAsync(AnnouncementListItem item)
    {
        if (string.IsNullOrEmpty(GroupId))
        {
            return Task.CompletedTask;
        }

        return _navigation.GoToAsync(BuildEditRoute(GroupId, GroupName, item.Id));
    }

    private async Task DeleteItemAsync(AnnouncementListItem item)
    {
        if (IsBusy || string.IsNullOrEmpty(GroupId))
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete announcement",
            "Delete \"" + item.Title + "\"? Members will no longer see it.",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        bool deleted;
        try
        {
            OperationResult result = await _announcementService.DeleteAsync(GroupId, item.Id);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                deleted = false;
            }
            else
            {
                deleted = true;
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (deleted)
        {
            _toasts.Show("Announcement deleted");
            await LoadAsync();
        }
    }

    // Member: leave this group, then go back to the previous page (Home or Discover).
    [RelayCommand]
    private async Task LeaveGroupAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(GroupId))
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Leave group",
            "Leave \"" + GroupName + "\"? You will no longer see its announcements. You can join again later.",
            "Leave",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        bool left;
        try
        {
            OperationResult result = await _membershipService.LeaveGroupAsync(GroupId);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                left = false;
            }
            else
            {
                left = true;
            }
        }
        finally
        {
            IsBusy = false;
        }

        if (left)
        {
            string leftName = string.IsNullOrWhiteSpace(GroupName) ? "the group" : GroupName;
            await _navigation.GoToAsync("..");

            // After going back: the previous page shows it (or keeps it until it appears).
            _toasts.Show("Left " + leftName);
        }
    }

    private static string BuildEditRoute(string groupId, string? groupName, string? announcementId)
    {
        string route = Routes.AnnouncementEdit
            + "?groupId=" + Uri.EscapeDataString(groupId)
            + "&groupName=" + Uri.EscapeDataString(groupName ?? string.Empty);

        if (!string.IsNullOrEmpty(announcementId))
        {
            route = route + "&announcementId=" + Uri.EscapeDataString(announcementId);
        }

        return route;
    }
}
