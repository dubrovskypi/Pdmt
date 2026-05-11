using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pdmt.Api.Data;
using Pdmt.Api.Infrastructure;
using Pdmt.Api.Infrastructure.Metrics;
using Pdmt.Api.Infrastructure.Options;
using Pdmt.Api.Middleware;
using Pdmt.Api.Services;
using StackExchange.Redis;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});

// Fail fast if required secrets are not configured
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. " +
        "Dev: dotnet user-secrets set \"Jwt:Secret\" \"<value>\"  " +
        "Prod: set env var Jwt__Secret");

var signingCredentials = new SigningCredentials(
    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    SecurityAlgorithms.HmacSha256);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "Authorization",
        Scheme = "Bearer",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// Add MVC controllers (attribute routing)
builder.Services.AddControllers();
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (!builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
    throw new InvalidOperationException(
        "Cors:AllowedOrigins is not configured. " +
        "Prod: set env var Cors__AllowedOrigins__0, Cors__AllowedOrigins__1, ...");

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClients", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// Register DbContext
var pgCs = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(pgCs))
    throw new InvalidOperationException(
        "ConnectionStrings:Postgres is not configured. " +
        "Prod: set env var ConnectionStrings__Postgres");

var redisCs = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisCs))
    throw new InvalidOperationException(
        "ConnectionStrings:Redis is not configured. " +
        "Prod: set env var ConnectionStrings__Redis");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(pgCs));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = signingCredentials.Key
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (sub is not null)
                    Activity.Current?.SetTag("enduser.id", sub);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSingleton(signingCredentials);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisCs);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddHealthChecks()
    .AddNpgSql(pgCs, tags: ["ready"])
    .AddRedis(redisCs, tags: ["ready"]);
var otelEndpoint = builder.Configuration["OpenTelemetry:Endpoint"]
    ?? throw new InvalidOperationException(
        "OpenTelemetry:Endpoint is not configured. " +
        "Dev: add to appsettings.Development.json  " +
        "Prod: set env var OpenTelemetry__Endpoint");

var assembly = Assembly.GetEntryAssembly()!;
var assemblyName = assembly.GetName().Name ?? "Pdmt.Api";
var assemblyVersion = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: assemblyName, serviceVersion: assemblyVersion))
    .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri(otelEndpoint))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options => options.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(AuthMetrics.MeterName))
    .WithLogging(_ => { });
// Register application services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IInsightsService, InsightsService>();
builder.Services.AddScoped<RedisRateLimitService>();
builder.Services.AddScoped<InMemoryRateLimitService>();
builder.Services.AddScoped<IRateLimitService, CompositeRateLimitService>();
builder.Services.AddSingleton<AuthMetrics>();
// Register background services
builder.Services.AddHostedService<TokenCleanupBgService>();
builder.Services.AddHostedService<FailedLoginCleanupBgService>();

// Configurations
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var section = builder.Configuration.GetSection("ForwardedHeaders");
    if (section.GetValue<bool>("TrustAllProxies"))
    {
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        return;
    }
    var networks = section.GetSection("KnownNetworks").Get<string[]>() ?? [];
    foreach (var cidr in networks)
    {
        var parts = cidr.Split('/');
        options.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        var migLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = (int)Math.Pow(2, attempt - 1);
                migLogger.LogWarning(ex, "Migration attempt {Attempt}/{Max} failed, retrying in {Delay}s", attempt, maxAttempts, delay);
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Database not reachable after 5 attempts during startup", ex);
            }
        }
    }
}

// Enable middleware to serve generated Swagger as a JSON endpoint and the Swagger UI.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Configure the HTTP request pipeline.
app.UseCors("WebClients");
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

await app.RunAsync();

public partial class Program { }
