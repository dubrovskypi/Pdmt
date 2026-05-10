using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdmt.Maui.Services;

namespace Pdmt.Maui.ViewModels;

public partial class RegisterViewModel(AuthService authService, ITokenService tokenService) : ObservableObject
{
    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private Task GoToLoginAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter email and password";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await authService.RegisterAsync(Email, Password);
            await tokenService.SetTokensAsync(result.AccessToken, result.AccessTokenExpiresAt, result.RefreshToken);
            await Shell.Current.GoToAsync("//events");
        }
        catch (HttpRequestException ex) when (
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Account with this email already exists";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Network error. Please try again.";
        }
        catch
        {
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
