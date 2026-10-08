using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// What one card of an announcements list shows. Rebuilt whenever the list is reloaded.
// The Edit and Delete buttons are commands that live on the row itself, so the page
// needs no cross-binding to the view model.
public sealed class AnnouncementListItem
{
    // An announcement newer than this gets a "New" chip.
    private static readonly TimeSpan NewWindow = TimeSpan.FromHours(24);

    public string Id { get; private set; } = string.Empty;
    public string GroupId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string DateText { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsEdited { get; private set; }
    public bool IsNew { get; private set; }

    // True when the signed-in admin posted it (only then may they edit or delete it).
    public bool IsMine { get; private set; }

    // Admin view only: someone else's post. It has no edit / delete icons, and this chip says why.
    public bool IsByOtherAdmin { get; private set; }

    public ICommand? EditCommand { get; private set; }
    public ICommand? DeleteCommand { get; private set; }

    public bool IsDeleted
    {
        get { return !IsActive; }
    }

    // Edit and Delete icon buttons: only for my own announcements that are not deleted.
    public bool CanManage
    {
        get { return IsMine && IsActive; }
    }

    // The chips row above the title is hidden when there is no chip to show.
    public bool HasBadges
    {
        get { return IsNew || IsEdited || IsDeleted || IsByOtherAdmin; }
    }

    // A deleted announcement (admins only) is shown faded.
    public double CardOpacity
    {
        get { return IsActive ? 1.0 : 0.6; }
    }

    // Read by screen readers for the icon-only buttons.
    public string EditLabel
    {
        get { return "Edit " + Title; }
    }

    public string DeleteLabel
    {
        get { return "Delete " + Title; }
    }

    public static AnnouncementListItem FromAnnouncement(
        Announcement announcement,
        string? currentUid,
        Func<AnnouncementListItem, Task> onEdit,
        Func<AnnouncementListItem, Task> onDelete,
        bool isAdminView = false)
    {
        AnnouncementListItem item = new AnnouncementListItem();
        item.Id = announcement.Id;
        item.GroupId = announcement.GroupId;
        item.Title = announcement.Title;
        item.Message = announcement.Message;
        item.IsActive = announcement.IsActive;
        item.IsEdited = announcement.IsEdited;
        item.IsMine = !string.IsNullOrEmpty(currentUid) && announcement.CreatedBy == currentUid;
        item.IsByOtherAdmin = isAdminView && !item.IsMine;
        item.EditCommand = new AsyncRelayCommand(() => onEdit(item));
        item.DeleteCommand = new AsyncRelayCommand(() => onDelete(item));

        // A freshly written document can briefly have no server time yet.
        if (announcement.CreatedAt.Year < 2000)
        {
            item.DateText = "Just now";
            item.IsNew = announcement.IsActive;
        }
        else
        {
            item.DateText = FormatDate(announcement.CreatedAt.ToLocalTime().DateTime);
            item.IsNew = announcement.IsActive && DateTimeOffset.UtcNow - announcement.CreatedAt < NewWindow;
        }

        return item;
    }

    // "Today, 3:45 PM", "Yesterday, 9:10 AM", "12 Sep, 4:30 PM", "3 Jan 2025".
    private static string FormatDate(DateTime local)
    {
        DateTime today = DateTime.Now.Date;
        string time = local.ToString("t", CultureInfo.CurrentCulture);

        if (local.Date == today)
        {
            return "Today, " + time;
        }

        if (local.Date == today.AddDays(-1))
        {
            return "Yesterday, " + time;
        }

        if (local.Year == today.Year)
        {
            return local.ToString("d MMM", CultureInfo.CurrentCulture) + ", " + time;
        }

        return local.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }
}
