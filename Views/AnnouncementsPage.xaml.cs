using GroupAnnouncementApp.Services.Interfaces;
using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class AnnouncementsPage : ContentPage
{
    private readonly AnnouncementsViewModel _viewModel;
    private readonly IToastService _toasts;

    public AnnouncementsPage(AnnouncementsViewModel viewModel, IToastService toasts)
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
