using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class GroupMembersPage : ContentPage
{
    private readonly GroupMembersViewModel _viewModel;
    private readonly IToastService _toasts;

    public GroupMembersPage(GroupMembersViewModel viewModel, IToastService toasts)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _toasts = toasts;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Toast.Attach(_toasts);
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        Toast.Detach();
        base.OnDisappearing();
    }
}
