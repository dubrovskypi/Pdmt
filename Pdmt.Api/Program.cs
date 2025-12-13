using Microsoft.EntityFrameworkCore;
using Pdmt.Api.Data;
using Pdmt.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers (attribute routing)
builder.Services.AddControllers();

// Register DbContext
var dbProvider = builder.Configuration["Database:Provider"];

if (dbProvider == "SqlServer")
{
    builder.Services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("SqlServer")));
}
else if (dbProvider == "Postgres")
{
    builder.Services.AddDbContext<AppDbContext, PostgresAppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("Postgres")));
}
else
{
    throw new Exception("Unknown database provider");
}

// Register application services
builder.Services.AddScoped<IEventService, EventService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/", () => Results.Ok("Hello World!"));

app.Run();
