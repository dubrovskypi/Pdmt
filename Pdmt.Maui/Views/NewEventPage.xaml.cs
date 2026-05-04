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
#if ANDROID
        (Platform.CurrentActivity as Android.App.Activity)?.Window?
            .SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
#endif
        await ((NewEventViewModel)BindingContext).LoadTagsCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        (Platform.CurrentActivity as Android.App.Activity)?.Window?
            .SetSoftInputMode(Android.Views.SoftInput.AdjustPan);
#endif
    }
}
