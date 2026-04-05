using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class InsightsPage : ContentPage
{
    public InsightsPage(InsightsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((InsightsViewModel)BindingContext).LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ((InsightsViewModel)BindingContext).CancelLoad();
    }
}
