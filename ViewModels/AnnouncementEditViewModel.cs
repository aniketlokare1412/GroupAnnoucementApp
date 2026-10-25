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
// In "pick groups" mode the page opens straight away. Its "who is this for" section shows "N groups selected" and
// two pills, Groups and Communities. A pill (or the card) opens the full-screen audience picker on that tab; Done brings
// the choice back to this page, and going back from the picker changes nothing. Each group still receives its own copy.
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
    private readonly IAudiencePickerService _audiencePicker;
    private readonly IToastService _toasts;

    private bool _isLoaded;
    private bool _isSaving;

    // The audience chosen on this page (through the picker).
    private readonly AudienceSelection _audience;


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

    // The "who is this for" section: the card text and the two pills.
    [ObservableProperty]
    private string _selectedCountText = "No groups selected";

    [ObservableProperty]
    private string _audienceCaption = "Choose Groups or Communities below";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private bool _hasSelection;

    // Pill states: lit up only for the tab the current choice was made in.
    [ObservableProperty]
    private bool _isGroupsOn;

    [ObservableProperty]
    private bool _isGroupsOff = true;

    [ObservableProperty]
    private bool _isCommunitiesOn;

    [ObservableProperty]
    private bool _isCommunitiesOff = true;

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

    // ---- Ready-made announcements ----
    // New posts start from a title in the drop-down. Its message is fixed (not editable); the last choice
    // ("Custom announcement") lets the admin write everything. Editing a post stays free text.
    public List<string> TemplateTitles { get; } = MessageTemplates.DropDownTitles();

    public List<string> TimeOptions { get; } = ResolutionOptions.Labels();

    private bool _lastWasTemplate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    [NotifyPropertyChangedFor(nameof(IsTemplate))]
    [NotifyPropertyChangedFor(nameof(ShowTitleEntry))]
    [NotifyPropertyChangedFor(nameof(IsMessageReadOnly))]
    [NotifyPropertyChangedFor(nameof(MessageBoxHeight))]
    private int _templateIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCustomTime))]
    private int _timeIndex;

    [ObservableProperty]
    private string _customTime = string.Empty;

    // False while editing an existing announcement.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    [NotifyPropertyChangedFor(nameof(IsTemplate))]
    [NotifyPropertyChangedFor(nameof(ShowTitleEntry))]
    [NotifyPropertyChangedFor(nameof(IsMessageReadOnly))]
    [NotifyPropertyChangedFor(nameof(ShowCustomTime))]
    [NotifyPropertyChangedFor(nameof(MessageBoxHeight))]
    private bool _isNewPost = true;

    public bool IsCustom => IsNewPost && TemplateIndex == MessageTemplates.All.Count;

    public bool IsTemplate => IsNewPost && TemplateIndex >= 0 && TemplateIndex < MessageTemplates.All.Count;

    public bool ShowTitleEntry => !IsNewPost || IsCustom;

    public bool IsMessageReadOnly => IsNewPost && !IsCustom;

    // A template message is fixed text of about 4 to 6 lines, so its box only needs that much room.
    // A message the admin types (custom text, or editing an existing post) gets a little more.
    public double MessageBoxHeight => IsTemplate ? 120 : 150;

    public bool ShowCustomTime => IsNewPost && TimeIndex == ResolutionOptions.CustomIndex;

    partial void OnTemplateIndexChanged(int value)
    {
        ApplyTemplate();
    }

    partial void OnTimeIndexChanged(int value)
    {
        ApplyTemplate();
    }

    partial void OnCustomTimeChanged(string value)
    {
        ApplyTemplate();
    }

    // Fills title and message from the chosen template (and the chosen resolution time).
    private void ApplyTemplate()
    {
        if (!IsNewPost)
        {
            return;
        }

        if (IsTemplate)
        {
            MessageTemplate template = MessageTemplates.All[TemplateIndex];
            int minutes;
            ReadResolutionMinutes(out minutes);
            Title = template.Title;
            Message = MessageTemplates.BuildBody(template, minutes);
        }
        else if (IsCustom && _lastWasTemplate)
        {
            // Switching to a custom announcement starts empty.
            Title = string.Empty;
            Message = string.Empty;
        }

        _lastWasTemplate = IsTemplate;
    }

    // The chosen resolution time in minutes (0 = not set). Returns false when the custom time is not valid.
    private bool ReadResolutionMinutes(out int minutes)
    {
        minutes = 0;
        if (TimeIndex == ResolutionOptions.CustomIndex)
        {
            return ResolutionOptions.TryParseHoursMinutes(CustomTime, out minutes);
        }

        minutes = ResolutionOptions.MinutesAt(TimeIndex);
        return true;
    }

    // Null when everything is fine, otherwise the message to show.
    private string? ValidateNewPostChoices(out int minutes)
    {
        minutes = 0;
        if (TemplateIndex < 0)
        {
            return "Choose a title first.";
        }

        if (!ReadResolutionMinutes(out minutes))
        {
            return "Enter the resolution time as HH:MM, for example 01:15 (at most 7 days).";
        }

        if (IsTemplate && MessageTemplates.All[TemplateIndex].TimeKind != TemplateTime.None && minutes == 0)
        {
            return "Choose the resolution time for this message.";
        }

        return null;
    }

    // One optional image or document for a NEW announcement (not offered when editing).
    private AttachmentPick? _attachment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAttachment))]
    [NotifyPropertyChangedFor(nameof(AttachmentSummary))]
    private string _attachmentName = string.Empty;

    [ObservableProperty]
    private bool _canAttach = true;

    public bool HasAttachment => AttachmentName.Length > 0;

    public string AttachmentSummary => HasAttachment ? AttachmentName : "Add an image or document (optional)";

    [RelayCommand]
    private async Task PickAttachmentAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            PickOptions options = new PickOptions();
            options.PickerTitle = "Choose an image or document";
            options.FileTypes = AttachmentLimits.PickerTypes;

            FileResult? file = await FilePicker.Default.PickAsync(options);
            if (file == null)
            {
                return;
            }

            using Stream stream = await file.OpenReadAsync();
            using MemoryStream buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            string contentType = string.IsNullOrEmpty(file.ContentType) ? AttachmentLimits.GuessContentType(file.FileName) : file.ContentType;
            byte[] bytes = buffer.ToArray();
            string fileName = file.FileName;
            bool isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            if (bytes.Length > AttachmentLimits.MaxBytes)
            {
                if (!isImage)
                {
                    ErrorMessage = "Documents can be up to " + AttachmentLimits.MaxKilobytes + " KB. This file is " + (bytes.Length / 1024) + " KB.";
                    return;
                }

                // Photos are shrunk to fit.
                byte[]? smaller = await Task.Run(() => ImageShrinker.Shrink(bytes, AttachmentLimits.MaxBytes));
                if (smaller == null)
                {
                    ErrorMessage = "This image is too large to attach. Please choose a smaller one.";
                    return;
                }

                bytes = smaller;
                contentType = "image/jpeg";
                fileName = Path.GetFileNameWithoutExtension(fileName) + ".jpg";
            }

            AttachmentPick pick = new AttachmentPick();
            pick.FileName = fileName;
            pick.ContentType = contentType;
            pick.Bytes = bytes;

            _attachment = pick;
            AttachmentName = pick.FileName;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("PICK ATTACHMENT ERROR: " + ex);
            ErrorMessage = "We could not read that file. Please try another one.";
        }
    }

    [RelayCommand]
    private void ClearAttachment()
    {
        _attachment = null;
        AttachmentName = string.Empty;
    }

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
        IAudiencePickerService audiencePicker,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _announcementService = announcementService;
        _groupService = groupService;
        _audiencePicker = audiencePicker;
        _toasts = toasts;
        _audience = new AudienceSelection(groupService, audiencePicker);

        TitleCountText = BuildCountText(string.Empty, AnnouncementLimits.MaxTitleLength);
        MessageCountText = BuildCountText(string.Empty, AnnouncementLimits.MaxMessageLength);
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
            CanAttach = false;
            IsNewPost = false;
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
            // New announcement for several groups: the page opens at once, the audience is chosen here.
            TitleText = "New announcement";
            IsPickerVisible = true;
            RefreshAudience();
            _isLoaded = true;
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

    [RelayCommand]
    private Task RetryAsync()
    {
        return LoadAsync();
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
        int resolveMinutes = 0;
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
                string? choiceProblem = ValidateNewPostChoices(out resolveMinutes);
                if (choiceProblem != null)
                {
                    ErrorMessage = choiceProblem;
                    return;
                }

                List<string> groupIds = new List<string>();
                if (IsSingleGroup)
                {
                    groupIds.Add(GroupId!);
                }
                else
                {
                    groupIds.AddRange(_audience.GroupIds);
                }

                if (groupIds.Count == 0)
                {
                    ErrorMessage = "Choose who this is for: tap Groups or Communities.";
                    return;
                }

                OperationResult<PublishResult> result;
                using (PerfTimer.Start("Post.Publish (" + groupIds.Count + " groups)"))
                {
                    result = await _announcementService.PublishAsync(Title, Message, groupIds, _attachment, resolveMinutes);
                }

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
        for (int i = 0; i < published.FailedGroupIds.Count; i++)
        {
            failedNames.Add(_audience.NameOf(published.FailedGroupIds[i]));
        }

        _audience.RemoveGroups(published.PostedGroupIds);
        RefreshAudience();

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

    // Shows the chosen audience on the card, lights the right pill and updates the main button.
    private void RefreshAudience()
    {
        SelectedCountText = _audience.CountText;
        AudienceCaption = _audience.CaptionText;
        HasSelection = _audience.Count > 0;
        IsGroupsOn = HasSelection && _audience.Kind == 0;
        IsGroupsOff = !IsGroupsOn;
        IsCommunitiesOn = HasSelection && _audience.Kind == 1;
        IsCommunitiesOff = !IsCommunitiesOn;
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
            int count = _audience.Count;
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

    private static string BuildCountText(string? text, int max)
    {
        int length = text == null ? 0 : text.Length;
        return length + "/" + max;
    }
}
