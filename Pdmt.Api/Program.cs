using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pdmt.Api.Data;
using Pdmt.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers (attribute routing)
builder.Services.AddControllers();

// Register DbContext
var dbProvider = builder.Configuration["Database:Provider"];

if (dbProvider == "SqlServer")
{
    builder.Services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
}
else if (dbProvider == "Postgres")
{
    builder.Services.AddDbContext<AppDbContext, PostgresAppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
}
else
{
    throw new Exception("Unknown database provider");
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

// Register application services
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/", () => Results.Ok("Hello World!"));

app.Run();
