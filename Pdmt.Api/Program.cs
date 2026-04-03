using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClients", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!;
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// Register DbContext
var dbProvider = builder.Configuration["Database:Provider"];
if (dbProvider == "SqlServer")
{
    var sqlCs = builder.Configuration.GetConnectionString("SqlServer");
    if (string.IsNullOrWhiteSpace(sqlCs))
        throw new InvalidOperationException(
            "ConnectionStrings:SqlServer is not configured. " +
            "Dev: set in appsettings.Development.json  " +
            "Prod: set env var ConnectionStrings__SqlServer");

    builder.Services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options =>
        options.UseSqlServer(sqlCs));
    builder.Services.AddHealthChecks()
        .AddSqlServer(sqlCs);
}
else if (dbProvider == "Postgres")
{
    var pgCs = builder.Configuration.GetConnectionString("Postgres");
    if (string.IsNullOrWhiteSpace(pgCs))
        throw new InvalidOperationException(
            "ConnectionStrings:Postgres is not configured. " +
            "Prod: set env var ConnectionStrings__Postgres");

    builder.Services.AddDbContext<AppDbContext, PostgresAppDbContext>(options =>
        options.UseNpgsql(pgCs));
}
else
{
    throw new InvalidOperationException("Unknown database provider. Set Database:Provider to 'SqlServer' or 'Postgres'.");
}

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
    var cs = builder.Configuration.GetValue<string>("Redis:ConnectionString");
    var config = ConfigurationOptions.Parse(cs!);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddHealthChecks().AddRedis(builder.Configuration.GetValue<string>("Redis:ConnectionString"));
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
builder.Services.AddScoped<RedisRateLimitService>();
builder.Services.AddScoped<InMemoryRateLimitService>();
builder.Services.AddScoped<IRateLimitService, CompositeRateLimitService>();
// Register background services
//builder.Services.AddHostedService<TokenCleanupBgService>(); //uncoment when cleanup will be needed

// Configurations
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"));

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint and the Swagger UI.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<HttpLoggingMiddleware>();
// Configure the HTTP request pipeline.
app.UseCors("WebClients");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok("Hello World!"));

app.Run();

public partial class Program { }
