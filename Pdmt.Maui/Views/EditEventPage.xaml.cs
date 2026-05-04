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
#if ANDROID
        (Platform.CurrentActivity as Android.App.Activity)?.Window?
            .SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
#endif
        await ((EditEventViewModel)BindingContext).LoadCommand.ExecuteAsync(null);
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
