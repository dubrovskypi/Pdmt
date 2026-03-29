using Pdmt.Maui.Services;
using Pdmt.Maui.ViewModels;

namespace Pdmt.Maui.Views;

public partial class LoginPage : ContentPage
{
    private readonly ITokenService _tokenService;

    public LoginPage(LoginViewModel viewModel, ITokenService tokenService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _tokenService = tokenService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _tokenService.IsAuthenticatedAsync())
            await Shell.Current.GoToAsync("//events");
    }
}
