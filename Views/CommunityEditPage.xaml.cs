using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class CommunityEditPage : ContentPage
{
    private readonly CommunityEditViewModel _viewModel;

    public CommunityEditPage(CommunityEditViewModel viewModel)
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
