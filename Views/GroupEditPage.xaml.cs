using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class GroupEditPage : ContentPage
{
    private readonly GroupEditViewModel _viewModel;

    public GroupEditPage(GroupEditViewModel viewModel)
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
