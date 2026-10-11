using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroupAnnouncementApp.Helpers;
using GroupAnnouncementApp.Models;
using GroupAnnouncementApp.Services.Interfaces;

namespace GroupAnnouncementApp.ViewModels;

public partial class AdminRequestsViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly ISessionService _session;
    private readonly IAdminRequestService _requests;
    private readonly IDialogService _dialogs;
    private readonly IToastService _toasts;

    private bool _hasLoaded;

    public ObservableCollection<AdminRequestItem> Items { get; } = new ObservableCollection<AdminRequestItem>();

    [ObservableProperty]
    private bool _hasNoItems;

    [ObservableProperty]
    private bool _showLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    public AdminRequestsViewModel(
        INavigationService navigation,
        ISessionService session,
        IAdminRequestService requests,
        IDialogService dialogs,
        IToastService toasts)
    {
        _navigation = navigation;
        _session = session;
        _requests = requests;
        _dialogs = dialogs;
        _toasts = toasts;
    }

    // Called by the page every time it appears.
    public async Task LoadAsync()
    {
        if (!_session.IsAdmin)
        {
            await _navigation.GoToAsync(Routes.Splash);
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        ShowLoading = !_hasLoaded;

        try
        {
            OperationResult<List<AdminRequest>> result = await _requests.GetPendingAsync();
            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            List<AdminRequest> list = result.Value ?? new List<AdminRequest>();
            Items.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                Items.Add(AdminRequestItem.From(list[i], ApproveAsync, DeclineAsync));
            }

            _hasLoaded = true;
            HasNoItems = Items.Count == 0;
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

    private Task ApproveAsync(AdminRequestItem item)
    {
        return DecideAsync(item, true);
    }

    private Task DeclineAsync(AdminRequestItem item)
    {
        return DecideAsync(item, false);
    }

    private async Task DecideAsync(AdminRequestItem item, bool approve)
    {
        if (IsBusy)
        {
            return;
        }

        string title = approve ? "Make administrator" : "Decline request";
        string message = approve
            ? "Make " + item.Phone + " an administrator? Administrators can manage users, groups and announcements."
            : "Decline the request from " + item.Phone + "?";

        bool confirmed = await _dialogs.ConfirmAsync(title, message, approve ? "Approve" : "Decline", "Cancel");
        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            OperationResult result = approve
                ? await _requests.ApproveAsync(item.Id)
                : await _requests.DeclineAsync(item.Id);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _toasts.Show(approve ? item.Phone + " is now an administrator" : "Request declined");
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }
}
