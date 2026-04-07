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
using Pdmt.Api.Middleware;
using Pdmt.Api.Services;
using StackExchange.Redis;
using System.Reflection;
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(redisCs);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddHealthChecks()
    .AddNpgSql(pgCs)
    .AddRedis(redisCs);
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
        .AddRuntimeInstrumentation())
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
// Register background services
//builder.Services.AddHostedService<TokenCleanupBgService>(); //uncoment when cleanup will be needed

// Configurations
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Enable middleware to serve generated Swagger as a JSON endpoint and the Swagger UI.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<HttpLoggingMiddleware>();
// Configure the HTTP request pipeline.
app.UseCors("WebClients");
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
