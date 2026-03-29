using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class NewEventPage : ContentPage
{
    public NewEventPage(NewEventViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((NewEventViewModel)BindingContext).LoadTagsCommand.ExecuteAsync(null);
    }
}
