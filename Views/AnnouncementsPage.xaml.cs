using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class AnnouncementsPage : ContentPage
{
    private readonly AnnouncementsViewModel _viewModel;

    public AnnouncementsPage(AnnouncementsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
