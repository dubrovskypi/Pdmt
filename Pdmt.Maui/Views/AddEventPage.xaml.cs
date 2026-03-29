using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class AddEventPage : ContentPage
{
    private readonly AddEventViewModel _viewModel;

    public AddEventPage(AddEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadTagsCommand.ExecuteAsync(null);
    }
}
