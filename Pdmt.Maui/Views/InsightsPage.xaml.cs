using Pdmt.Maui.ViewModels;
using Pdmt.Maui.ViewModels.Cards;

namespace Pdmt.Maui.Views;

public partial class InsightsPage : ContentPage
{
    public InsightsPage(InsightsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.ScrollToCardRequested += OnScrollToCardRequested;
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

    private void OnScrollToCardRequested(int index)
    {
        var vm = (InsightsViewModel)BindingContext;
        if (index < vm.Cards.Count)
            InsightsCarousel.ScrollTo(vm.Cards[index], position: ScrollToPosition.Center, animate: true);
    }

    private void OnCarouselPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        ((InsightsViewModel)BindingContext).SetCurrentPosition(e.CurrentPosition);
    }
}
