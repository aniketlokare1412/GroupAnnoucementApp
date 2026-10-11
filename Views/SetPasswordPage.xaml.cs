using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class SetPasswordPage : ContentPage
{
    private readonly SetPasswordViewModel _viewModel;

    public SetPasswordPage(SetPasswordViewModel viewModel)
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
