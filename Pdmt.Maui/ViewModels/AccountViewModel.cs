using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class AccountViewModel(
    AuthService authService,
    ITokenService tokenService) : ObservableObject
{
    [RelayCommand]
    private async Task LogoutAsync()
    {
        try { await authService.LogoutAsync(); }
        catch { /* ignore — clear tokens anyway */ }

        await tokenService.ClearAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
