using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class MyGroupsPage : ContentPage
{
    private readonly MyGroupsViewModel _viewModel;

    public MyGroupsPage(MyGroupsViewModel viewModel)
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
