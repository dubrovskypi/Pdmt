using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class AccountPage : ContentPage
{
    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccountViewModel vm) vm.LoadCommand.Execute(null);
    }
}
