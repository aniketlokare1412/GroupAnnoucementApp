using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class AnnouncementGroupsPage : ContentPage
{
    private readonly AnnouncementGroupsViewModel _viewModel;

    public AnnouncementGroupsPage(AnnouncementGroupsViewModel viewModel)
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
