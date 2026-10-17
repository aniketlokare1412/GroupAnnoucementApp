using CommunityToolkit.Mvvm.ComponentModel;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;

namespace GroupAnnouncementApp.ViewModels;

// Additions to AnnouncementsViewModel (the class is partial, so the original file is unchanged):
//  - Delete a cancelled outage message, so members see the regular announcement again.
//  - Coloured group tile in the page header.
public partial class AnnouncementsViewModel
{
    [ObservableProperty]
    private Color _tileBackground = TileColors.Background(null);

    [ObservableProperty]
    private Color _tileForeground = TileColors.Foreground(null);

    // Runs whenever the header initials are set (LoadAsync sets them after GroupName is known).
    partial void OnInitialsChanged(string value)
    {
        TileBackground = TileColors.Background(GroupName);
        TileForeground = TileColors.Foreground(GroupName);
    }

    // Runs whenever the outage card is replaced: give it its delete action.
    partial void OnOutageChanged(OutageCardItem? value)
    {
        value?.AttachDelete(DeleteOutageAsync);
    }

    private async Task DeleteOutageAsync()
    {
        if (IsBusy || string.IsNullOrEmpty(GroupId))
        {
            return;
        }

        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete outage message",
            "Delete the cancelled-outage message? Members will see the regular announcement again.",
            "Delete",
            "Keep it");
        if (!confirmed)
        {
            return;
        }

        OperationResult result = await _outageService.DeleteAsync(GroupId);
        if (!result.IsSuccess)
        {
            _toasts.Show(result.ErrorMessage ?? "We could not delete the outage message.");
            return;
        }

        _toasts.Show("Outage message deleted");
        await LoadOutageAsync();
    }
}
