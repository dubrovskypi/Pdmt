using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class AddEventPage : ContentPage
{
    public AddEventPage(AddEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((AddEventViewModel)BindingContext).LoadTagsCommand.ExecuteAsync(null);
    }
}
