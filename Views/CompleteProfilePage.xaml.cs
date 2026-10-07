using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class CompleteProfilePage : ContentPage
{
    private readonly CompleteProfileViewModel _viewModel;

    public CompleteProfilePage(CompleteProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Prepare();
    }
}
