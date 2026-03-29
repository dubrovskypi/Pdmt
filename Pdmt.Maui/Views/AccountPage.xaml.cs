using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class AccountPage : ContentPage
{
    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
