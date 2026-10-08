using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class JoinGroupsPage : ContentPage
{
    private readonly JoinGroupsViewModel _viewModel;
    private readonly IToastService _toasts;

    public JoinGroupsPage(JoinGroupsViewModel viewModel, IToastService toasts)
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
