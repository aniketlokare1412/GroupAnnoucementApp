using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// Admin: plan an outage for one or more groups. The date, the from and to time, and when members
// should start seeing the message (now, or later). The page opens straight away; its "Send to" section shows
// "N groups selected" and the Groups / Communities pills that open the audience picker.
public partial class OutageEditViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IOutageService _outageService;
    private readonly IGroupAdminService _groupService;
    private readonly IAudiencePickerService _audiencePicker;
    private readonly IToastService _toasts;

    private bool _isLoaded;

    // The audience chosen on this page (through the picker).
    private readonly AudienceSelection _audience;

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

    // The "Send to" section: the card text and the two pills.
    [ObservableProperty]
    private string _selectedCountText = "No groups selected";

    [ObservableProperty]
    private string _audienceCaption = "Choose Groups or Communities below";

    [ObservableProperty]
    private bool _isGroupsOn;

    [ObservableProperty]
    private bool _isGroupsOff = true;

    [ObservableProperty]
    private bool _isCommunitiesOn;

    [ObservableProperty]
    private bool _isCommunitiesOff = true;

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
        _audience = new AudienceSelection(groupService, audiencePicker);
    }

    // Called by the page every time it appears.
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (_isLoaded)
        {
            return;
        }

        // The page opens at once; the audience is chosen here with the Groups / Communities pills.
        RefreshAudience();
        _isLoaded = true;
    }

    // The Groups / Communities pills: open the picker on that tab.
    [RelayCommand]
    private async Task ChooseAudienceAsync(string? tab)
    {
        if (IsBusy || !_isLoaded)
        {
            return;
        }

        ErrorMessage = null;
        string? problem = await _audience.ChooseAsync(tab == "1" ? 1 : 0);
        if (problem != null)
        {
            ErrorMessage = problem;
        }

        RefreshAudience();
    }

    // The "N groups selected" card: opens the picker on the tab the choice came from.
    [RelayCommand]
    private Task EditAudienceAsync()
    {
        return ChooseAudienceAsync(_audience.Kind == 1 ? "1" : "0");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        List<string> chosen = new List<string>(_audience.GroupIds);
        if (chosen.Count == 0)
        {
            ErrorMessage = "Choose who this is for: tap Groups or Communities.";
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
            List<string> done = new List<string>();
            for (int i = 0; i < chosen.Count; i++)
            {
                OperationResult result = await _outageService.ScheduleAsync(chosen[i], start, end, publishAt);
                if (result.IsSuccess)
                {
                    done.Add(chosen[i]);
                }
                else
                {
                    failed.Add(_audience.NameOf(chosen[i]) + ": " + result.ErrorMessage);
                }
            }

            if (failed.Count > 0)
            {
                // The groups that already have the outage are dropped, so Schedule again only retries the rest.
                _audience.RemoveGroups(done);
                RefreshAudience();
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

    // Shows the chosen audience on the card and lights the right pill.
    private void RefreshAudience()
    {
        SelectedCountText = _audience.CountText;
        AudienceCaption = _audience.CaptionText;
        bool has = _audience.Count > 0;
        IsGroupsOn = has && _audience.Kind == 0;
        IsGroupsOff = !IsGroupsOn;
        IsCommunitiesOn = has && _audience.Kind == 1;
        IsCommunitiesOff = !IsCommunitiesOn;
    }
}
