using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class OutageEditPage : ContentPage
{
    private readonly OutageEditViewModel _viewModel;

    public OutageEditPage(OutageEditViewModel viewModel)
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
