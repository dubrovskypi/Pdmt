using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class LoginViewModel(AuthService authService, ITokenService tokenService) : ObservableObject
{
    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter email and password";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await authService.LoginAsync(Email, Password);
            await tokenService.SetTokensAsync(result.AccessToken, result.RefreshToken);
            await Shell.Current.GoToAsync("//events");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Invalid email or password";
        }
        catch
        {
            ErrorMessage = "Network error. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
