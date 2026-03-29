using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class EventListPage : ContentPage
{
    public EventListPage(EventListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((EventListViewModel)BindingContext).LoadCommand.ExecuteAsync(null);
    }
}
