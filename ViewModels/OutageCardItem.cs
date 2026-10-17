using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// The planned outage (or its cancellation) shown to an admin on the group's announcements page.
public sealed class OutageCardItem
{
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string WindowText { get; private set; } = string.Empty;
    public bool CanCancel { get; private set; }
    public ICommand? CancelCommand { get; private set; }

    // A cancelled outage message can be deleted, so members see the regular announcement again.
    public bool CanDelete { get; private set; }
    public ICommand? DeleteCommand { get; private set; }

    // Stripe colour on the card: amber while planned, green once cancelled (power stays on).
    public Color AccentColor { get; private set; } = Color.FromArgb("#F59E0B");

    public static OutageCardItem From(Outage outage, Func<Task> onCancel)
    {
        bool cancelled = outage.Status == OutageText.Cancelled;
        DateTimeOffset start = DateTimeOffset.FromUnixTimeMilliseconds(outage.StartMs);
        DateTimeOffset end = DateTimeOffset.FromUnixTimeMilliseconds(outage.EndMs);
        DateTimeOffset publishAt = DateTimeOffset.FromUnixTimeMilliseconds(outage.PublishAtMs);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        OutageCardItem item = new OutageCardItem();
        item.Title = cancelled ? "Outage cancelled" : "Planned outage";
        item.Message = cancelled ? OutageText.BuildCancelled(start, end) : OutageText.BuildPlanned(start, end, now);
        item.AccentColor = Color.FromArgb(cancelled ? "#16A34A" : "#F59E0B");

        string from = publishAt.ToLocalTime().ToString("dd MMM, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string until = end.ToLocalTime().ToString("dd MMM, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        item.WindowText = publishAt > now
            ? "Members will see this from " + from + " until " + until
            : "Members see this until " + until;

        item.CanCancel = !cancelled;
        item.CanDelete = cancelled;
        item.CancelCommand = new AsyncRelayCommand(onCancel);
        return item;
    }

    // Called by the view model that owns the card (see AnnouncementsViewModel.Extras.cs).
    public void AttachDelete(Func<Task> onDelete)
    {
        DeleteCommand = new AsyncRelayCommand(onDelete);
    }
}
