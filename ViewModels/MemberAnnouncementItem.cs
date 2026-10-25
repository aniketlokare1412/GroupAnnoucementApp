using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

// One announcement card on the member Home page (the latest one of a joined group).
public sealed class MemberAnnouncementItem : ObservableObject
{
    // The group this card belongs to (used to place the card under its community headings).
    public string GroupId { get; private set; } = string.Empty;
    public string GroupNumberText { get; private set; } = string.Empty;
    public string GroupName { get; private set; } = string.Empty;
    public string GroupInitials { get; private set; } = string.Empty;
    public Color TileBackground { get; private set; } = TileColors.Background(null);
    public Color TileForeground { get; private set; } = TileColors.Foreground(null);
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;

    // False for the general notice shown to a member who has not joined any group (no group header).
    public bool HasGroup { get; private set; } = true;

    // "Also in: Community 2, Community 3" - shown once on the card when its group is in several communities.
    public string AlsoInText { get; set; } = string.Empty;

    public bool HasAlsoIn
    {
        get { return !string.IsNullOrEmpty(AlsoInText); }
    }

    // The "leave group" icon in the card header. Only one card per group shows it.
    public bool ShowLeave { get; set; }

    public ICommand? LeaveCommand { get; set; }

    // The 5-minute notice has no title line.
    public bool HasTitle
    {
        get { return !string.IsNullOrEmpty(Title); }
    }
    public string DateText { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private bool _isNotice;

    private ImageSource? _image;

    // The stored image, filled in a moment after the card appears (see LoadImageAsync).
    public ImageSource? Image
    {
        get { return _image; }
        private set { SetProperty(ref _image, value); }
    }

    public bool HasImage { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public bool HasFile { get; private set; }
    public ICommand? OpenFileCommand { get; private set; }

    // Used by an outage card (it has no Extras).
    private Color _fallbackAccent = Color.FromArgb("#F59E0B");

    private AnnouncementExtras? _extras;

    // Status, resolution time, panic button. Set by the view model that builds the card.
    public AnnouncementExtras? Extras
    {
        get { return _extras; }
        set
        {
            if (_extras != null)
            {
                _extras.PropertyChanged -= OnExtrasChanged;
            }

            _extras = value;
            if (_extras != null)
            {
                _extras.PropertyChanged += OnExtrasChanged;
            }

            OnPropertyChanged(nameof(AccentColor));
        }
    }

    // The status parts of the card are only shown for a real announcement (not for an outage card).
    public bool HasExtras
    {
        get { return _extras != null; }
    }

    // The status row (Work in progress, timer ...) is for real announcements; the 5-minute notice only has the panic button.
    public bool HasStatusRow
    {
        get { return _extras != null && !_isNotice; }
    }

    // Stripe colour: red = work in progress, amber = time is up (or planned outage), green = completed / restored.
    public Color AccentColor
    {
        get { return _extras != null && !_isNotice ? _extras.AccentColor : _fallbackAccent; }
    }

    private void OnExtrasChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AccentColor));
    }

    private string _fileGroupId = string.Empty;
    private string _fileAnnouncementId = string.Empty;
    private string _sharedFileId = string.Empty;

    public string FileLabel
    {
        get { return "Attached file " + FileName; }
    }

    // An image shows inside the card; a document is a tappable row that opens the file.
    private void ConfigureAttachment(string groupId, string announcementId, Announcement announcement, IAttachmentService? attachments)
    {
        _fileGroupId = groupId;
        _fileAnnouncementId = announcementId;
        _sharedFileId = announcement.SharedFileId;
        HasImage = announcement.AttachmentKind == "image";
        HasFile = announcement.AttachmentKind == "file";

        if (HasFile)
        {
            FileName = string.IsNullOrEmpty(announcement.AttachmentName) ? "Attachment" : announcement.AttachmentName;
            if (attachments != null)
            {
                OpenFileCommand = new AsyncRelayCommand(() => AttachmentOpener.OpenAsync(attachments, groupId, announcementId, announcement.SharedFileId));
            }
        }
    }

    // Fetches the stored image (if any). Call without waiting, after the card is in the list.
    public async Task LoadImageAsync(IAttachmentService attachments)
    {
        if (!HasImage)
        {
            return;
        }

        OperationResult<AttachmentFile> result = string.IsNullOrEmpty(_sharedFileId)
            ? await attachments.GetAsync(_fileGroupId, _fileAnnouncementId)
            : await attachments.GetSharedAsync(_sharedFileId);
        if (!result.IsSuccess || result.Value == null)
        {
            return;
        }

        byte[] bytes = result.Value.Bytes;
        Image = ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    public string CardLabel
    {
        get
        {
            if (_isNotice)
            {
                return (HasGroup ? "Notice for " + GroupName + ": " : "Notice: ") + Message;
            }

            return "Announcement in " + GroupName + ": " + Title + ". " + DateText.Replace("\n", " ");
        }
    }

    private void ApplyGroup(AnnouncementGroup group)
    {
        GroupId = group.Id;
        GroupNumberText = "No. " + (string.IsNullOrWhiteSpace(group.Number) ? "-" : group.Number);
        GroupName = group.Name;
        GroupInitials = InitialsHelper.From(group.Name);
        TileBackground = TileColors.Background(group.Name);
        TileForeground = TileColors.Foreground(group.Name);
    }

    // A planned outage, or its cancellation: shown like an announcement, without panic button, timer or status.
    public static MemberAnnouncementItem FromOutage(AnnouncementGroup group, Outage outage, DateTimeOffset now)
    {
        bool cancelled = outage.Status == OutageText.Cancelled;
        DateTimeOffset start = DateTimeOffset.FromUnixTimeMilliseconds(outage.StartMs);
        DateTimeOffset end = DateTimeOffset.FromUnixTimeMilliseconds(outage.EndMs);
        DateTimeOffset published = DateTimeOffset.FromUnixTimeMilliseconds(outage.PublishAtMs);

        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.ApplyGroup(group);
        item.Title = cancelled ? "Outage cancelled" : "Planned outage";
        item.Message = cancelled ? OutageText.BuildCancelled(start, end) : OutageText.BuildPlanned(start, end, now);
        item._fallbackAccent = Color.FromArgb(cancelled ? "#65A30D" : "#F59E0B");
        item.CreatedAt = published;
        item.DateText = published.ToLocalTime().ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
            + "\n" + published.ToLocalTime().ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        return item;
    }

    // The standing "wait 5 minutes" message of a group that has no active message from the admin.
    // CreatedAt is the oldest possible time, so it always sits below the real messages.
    public static MemberAnnouncementItem FromFiveMinuteNotice(AnnouncementGroup group)
    {
        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.ApplyGroup(group);
        item.Title = string.Empty;
        item.Message = MessageTemplates.FiveMinuteNotice;
        item._fallbackAccent = Color.FromArgb("#0F766E");
        item.CreatedAt = DateTimeOffset.MinValue;
        item.DateText = string.Empty;
        item._isNotice = true;
        return item;
    }

    // The same 5-minute message for a member who has not joined any group yet. No group header.
    public static MemberAnnouncementItem FromGeneralNotice()
    {
        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.HasGroup = false;
        item.Title = string.Empty;
        item.Message = MessageTemplates.FiveMinuteNotice;
        item._fallbackAccent = Color.FromArgb("#0F766E");
        item.CreatedAt = DateTimeOffset.MinValue;
        item.DateText = string.Empty;
        item._isNotice = true;
        return item;
    }

    public static MemberAnnouncementItem From(AnnouncementGroup group, Announcement announcement, IAttachmentService? attachments = null)
    {
        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.ApplyGroup(group);
        item.Title = announcement.Title;
        item.Message = announcement.Message;
        // A message just posted can still have no server time: it is the newest.
        item.CreatedAt = announcement.CreatedAt == default ? DateTimeOffset.UtcNow : announcement.CreatedAt;
        item.ConfigureAttachment(group.Id, announcement.Id, announcement, attachments);
        item.DateText = announcement.CreatedAt == default
            ? "Just now"
            : announcement.CreatedAt.ToLocalTime().ToString("dd MMM yyyy") + "\n" + announcement.CreatedAt.ToLocalTime().ToString("hh:mm tt");
        return item;
    }
}
