using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class WeeklyCalendarPage : ContentPage
{
    public WeeklyCalendarPage(WeeklyCalendarViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((WeeklyCalendarViewModel)BindingContext).LoadCommand.ExecuteAsync(null);
    }
}
