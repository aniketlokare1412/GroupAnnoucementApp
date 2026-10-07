using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class GroupMembersPage : ContentPage
{
    private readonly GroupMembersViewModel _viewModel;

    public GroupMembersPage(GroupMembersViewModel viewModel)
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
