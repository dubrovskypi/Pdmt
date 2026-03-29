using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Pdmt.Client;
using Pdmt.Client.Configuration;
using Pdmt.Client.Services;
try
{
    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
    //configs
    builder.Services.Configure<PdmtApiOptions>(builder.Configuration.GetSection(PdmtApiOptions.SectionName));
    //services
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddTransient<AuthHeaderHandler>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<EventService>();
    builder.Services.AddScoped<TagService>();
    builder.Services.AddScoped<AnalyticsService>();
    //http client
    var apiOptions = builder.Configuration.GetSection(PdmtApiOptions.SectionName).Get<PdmtApiOptions>()!;
    builder.Services.AddHttpClient(apiOptions.ClientName, client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl);
    })
    .AddHttpMessageHandler<AuthHeaderHandler>();

    await builder.Build().RunAsync();
}
catch (Exception e)
{
    Console.WriteLine(e);
	throw;
}

