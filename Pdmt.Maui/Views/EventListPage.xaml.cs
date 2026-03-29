using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class EventListPage : ContentPage
{
    private readonly EventListViewModel _viewModel;

    public EventListPage(EventListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
