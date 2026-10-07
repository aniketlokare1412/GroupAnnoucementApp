using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one row of an announcements list shows. Rebuilt whenever the list is reloaded.
// The Edit and Delete buttons are commands that live on the row itself, so the page
// needs no cross-binding to the view model.
public sealed class AnnouncementListItem
{
    public string Id { get; private set; } = string.Empty;
    public string GroupId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsEdited { get; private set; }

    // True when the signed-in admin posted it (only then may they edit or delete it).
    public bool IsMine { get; private set; }

    public ICommand? EditCommand { get; private set; }
    public ICommand? DeleteCommand { get; private set; }

    public bool IsDeleted
    {
        get { return !IsActive; }
    }

    // Edit and Delete buttons: only for my own announcements that are not deleted.
    public bool CanManage
    {
        get { return IsMine && IsActive; }
    }

    public static AnnouncementListItem FromAnnouncement(
        Announcement announcement,
        string? currentUid,
        Func<AnnouncementListItem, Task> onEdit,
        Func<AnnouncementListItem, Task> onDelete)
    {
        AnnouncementListItem item = new AnnouncementListItem();
        item.Id = announcement.Id;
        item.GroupId = announcement.GroupId;
        item.Title = announcement.Title;
        item.Message = announcement.Message;
        item.IsActive = announcement.IsActive;
        item.IsEdited = announcement.IsEdited;
        item.IsMine = !string.IsNullOrEmpty(currentUid) && announcement.CreatedBy == currentUid;
        item.EditCommand = new AsyncRelayCommand(() => onEdit(item));
        item.DeleteCommand = new AsyncRelayCommand(() => onDelete(item));

        // A freshly written document can briefly have no server time yet.
        if (announcement.CreatedAt.Year < 2000)
        {
            item.DateText = "Just now";
        }
        else
        {
            item.DateText = announcement.CreatedAt.ToLocalTime().ToString("g");
        }

        return item;
    }
}
