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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
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

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<NewEventViewModel>();
        builder.Services.AddTransient<EventListViewModel>();
        builder.Services.AddTransient<WeeklyCalendarViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddTransient<InsightsViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<NewEventPage>();
        builder.Services.AddTransient<EventListPage>();
        builder.Services.AddTransient<WeeklyCalendarPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddTransient<InsightsPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

        return builder.Build();
    }
}
