using System.Reflection;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pdmt.Maui.Configuration;
using Pdmt.Maui.Services;
using Pdmt.Maui.ViewModels;
using Pdmt.Maui.Views;

namespace Pdmt.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => { })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Background", (handler, _) =>
                    handler.PlatformView.SetBackgroundResource(Resource.Drawable.entry_background));
                Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("Background", (handler, _) =>
                    handler.PlatformView.SetBackgroundResource(Resource.Drawable.entry_background));
                Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("Background", (handler, _) =>
                    handler.PlatformView.SetBackgroundResource(Resource.Drawable.entry_background));
                Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("Background", (handler, _) =>
                    handler.PlatformView.SetBackgroundResource(Resource.Drawable.entry_background));
                Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("Background", (handler, _) =>
                    handler.PlatformView.SetBackgroundResource(Resource.Drawable.entry_background));
                Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("SwitchColors", (handler, _) =>
                {
                    if (handler.PlatformView is not AndroidX.AppCompat.Widget.SwitchCompat sw) return;
                    var states = new int[][]
                    {
                        [Android.Resource.Attribute.StateChecked],
                        [-Android.Resource.Attribute.StateChecked]
                    };
                    sw.ThumbTintList = new Android.Content.Res.ColorStateList(states,
                        [Android.Graphics.Color.ParseColor("#006a60"), Android.Graphics.Color.White]);
                    sw.TrackTintList = new Android.Content.Res.ColorStateList(states,
                        [Android.Graphics.Color.ParseColor("#22c55e"), Android.Graphics.Color.ParseColor("#BBBBBB")]);
                });
                Microsoft.Maui.Handlers.ImageButtonHandler.Mapper.AppendToMapping("ImageTint", (handler, _) =>
                    handler.PlatformView.ImageTintList =
                        Android.Content.Res.ColorStateList.ValueOf(
                            Android.Graphics.Color.ParseColor("#111111")));
#endif
            });

        // Configuration from embedded resources
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("Pdmt.Maui.appsettings.json"))
        {
            if (stream is not null)
                builder.Configuration.AddJsonStream(stream);
        }

#if DEBUG
        using (var devStream = assembly.GetManifestResourceStream("Pdmt.Maui.appsettings.Development.json"))
        {
            if (devStream is not null)
                builder.Configuration.AddJsonStream(devStream);
        }
#endif

        builder.Services.Configure<PdmtApiOptions>(
            builder.Configuration.GetSection(PdmtApiOptions.SectionName));

        // Services
        builder.Services.AddTransient<AuthHeaderHandler>();
        builder.Services.AddSingleton<ITokenService, TokenService>();      
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<EventService>();
        builder.Services.AddSingleton<TagService>();
        builder.Services.AddSingleton<AnalyticsService>();
        builder.Services.AddSingleton<InsightsService>();

        // HttpClient
        builder.Services.AddHttpClient("PdmtApi", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PdmtApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
#if DEBUG
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
#endif
        .AddHttpMessageHandler<AuthHeaderHandler>();

        // Auth-only client — no AuthHeaderHandler (used for login/register/refresh/logout)
        builder.Services.AddHttpClient("PdmtAuth", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PdmtApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        })
#if DEBUG
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        })
#endif
        ;

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<NewEventViewModel>();
        builder.Services.AddTransient<EditEventViewModel>();
        builder.Services.AddTransient<EventListViewModel>();
        builder.Services.AddTransient<WeeklyCalendarViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddSingleton<InsightsViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<NewEventPage>();
        builder.Services.AddTransient<EditEventPage>();
        builder.Services.AddTransient<EventListPage>();
        builder.Services.AddTransient<WeeklyCalendarPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddSingleton<InsightsPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
