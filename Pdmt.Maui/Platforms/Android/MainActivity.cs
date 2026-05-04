using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace Pdmt.Maui;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Window is null) return;

        Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#f9f9f9"));
        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        controller.AppearanceLightStatusBars = true;
    }
}
