using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class AnnouncementEditPage : ContentPage
{
    private readonly AnnouncementEditViewModel _viewModel;

    public AnnouncementEditPage(AnnouncementEditViewModel viewModel)
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
