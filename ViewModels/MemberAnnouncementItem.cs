using GroupAnnouncementApp.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using GroupAnnouncementApp.Helpers;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// One announcement card on the member Home page (the latest one of a joined group).
public sealed class MemberAnnouncementItem : ObservableObject
{
    public string GroupNumberText { get; private set; } = string.Empty;
    public string GroupName { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

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

    // Status, resolution time, panic button and admin tools. Set by the view model that builds the card.
    public AnnouncementExtras? Extras { get; set; }

    private string _fileGroupId = string.Empty;
    private string _fileAnnouncementId = string.Empty;

    public string FileLabel
    {
        get { return "Attached file " + FileName; }
    }

    // An image shows inside the card; a document is a tappable row that opens the file.
    private void ConfigureAttachment(string groupId, string announcementId, Announcement announcement, IAttachmentService? attachments)
    {
        _fileGroupId = groupId;
        _fileAnnouncementId = announcementId;
        HasImage = announcement.AttachmentKind == "image";
        HasFile = announcement.AttachmentKind == "file";

        if (HasFile)
        {
            FileName = string.IsNullOrEmpty(announcement.AttachmentName) ? "Attachment" : announcement.AttachmentName;
            if (attachments != null)
            {
                OpenFileCommand = new AsyncRelayCommand(() => AttachmentOpener.OpenAsync(attachments, groupId, announcementId));
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

        OperationResult<AttachmentFile> result = await attachments.GetAsync(_fileGroupId, _fileAnnouncementId);
        if (!result.IsSuccess || result.Value == null)
        {
            return;
        }

        byte[] bytes = result.Value.Bytes;
        Image = ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    public string CardLabel
    {
        get { return "Announcement in " + GroupName + ": " + Title + ". " + DateText; }
    }

    // A planned outage, or its cancellation: shown like an announcement, without panic button, timer or status.
    public static MemberAnnouncementItem FromOutage(AnnouncementGroup group, Outage outage, DateTimeOffset now)
    {
        bool cancelled = outage.Status == OutageText.Cancelled;
        DateTimeOffset start = DateTimeOffset.FromUnixTimeMilliseconds(outage.StartMs);
        DateTimeOffset end = DateTimeOffset.FromUnixTimeMilliseconds(outage.EndMs);
        DateTimeOffset published = DateTimeOffset.FromUnixTimeMilliseconds(outage.PublishAtMs);

        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.GroupNumberText = "Group number: " + (string.IsNullOrWhiteSpace(group.Number) ? "-" : group.Number);
        item.GroupName = group.Name;
        item.Title = cancelled ? "Outage cancelled" : "Planned outage";
        item.Message = cancelled ? OutageText.BuildCancelled(start, end) : OutageText.BuildPlanned(start, end, now);
        item.CreatedAt = published;
        item.DateText = published.ToLocalTime().ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)
            + "\n" + published.ToLocalTime().ToString("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        return item;
    }

    public static MemberAnnouncementItem From(AnnouncementGroup group, Announcement announcement, IAttachmentService? attachments = null)
    {
        MemberAnnouncementItem item = new MemberAnnouncementItem();
        item.GroupNumberText = "Group number: " + (string.IsNullOrWhiteSpace(group.Number) ? "-" : group.Number);
        item.GroupName = group.Name;
        item.Title = announcement.Title;
        item.Message = announcement.Message;
        item.CreatedAt = announcement.CreatedAt;
        item.ConfigureAttachment(group.Id, announcement.Id, announcement, attachments);
        item.DateText = announcement.CreatedAt == default
            ? "Just now"
            : announcement.CreatedAt.ToLocalTime().ToString("dd MMM yyyy") + "\n" + announcement.CreatedAt.ToLocalTime().ToString("hh:mm tt");
        return item;
    }
}
