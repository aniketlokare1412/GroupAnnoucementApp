using GroupAnnouncementApp.ViewModels;

namespace GroupAnnouncementApp.Views;

public partial class StructurePage : ContentPage
{
    private readonly StructureViewModel _viewModel;

    public StructurePage(StructureViewModel viewModel)
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
