using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class EditEventPage : ContentPage
{
    public EditEventPage(EditEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((EditEventViewModel)BindingContext).LoadCommand.ExecuteAsync(null);
    }
}
