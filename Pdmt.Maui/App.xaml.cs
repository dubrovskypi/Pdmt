using Pdmt.Maui.Services;

namespace Pdmt.Maui;

public partial class App : Application
{
    public App(AppShell shell, ITokenService tokenService)
    {
        InitializeComponent();
        MainPage = shell;
        InitialNavigationAsync(tokenService).FireAndForget();
    }

    private static async Task InitialNavigationAsync(ITokenService tokenService)
    {
        // Give Shell time to fully initialize before navigating
        await Task.Delay(100);
        var isAuthenticated = await tokenService.IsAuthenticatedAsync();
        if (!isAuthenticated)
            await Shell.Current.GoToAsync("//login");
    }
}

file static class TaskExtensions
{
    public static void FireAndForget(this Task task) =>
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
                Console.WriteLine($"Unhandled exception: {t.Exception}");
        }, TaskScheduler.Default);
}
