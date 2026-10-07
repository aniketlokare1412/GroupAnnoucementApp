using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class AddMembersPage : ContentPage
{
    private readonly AddMembersViewModel _viewModel;

    public AddMembersPage(AddMembersViewModel viewModel)
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
