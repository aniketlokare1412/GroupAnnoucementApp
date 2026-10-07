using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class JoinGroupsPage : ContentPage
{
    private readonly JoinGroupsViewModel _viewModel;

    public JoinGroupsPage(JoinGroupsViewModel viewModel)
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
