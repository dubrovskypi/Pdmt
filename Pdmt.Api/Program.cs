using Pdmt.Api.Data;
using Pdmt.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MVC controllers (attribute routing)
builder.Services.AddControllers();

//// Register DbContext (example: in-memory for local/dev)
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseInMemoryDatabase("PdmtDb"));

// Register application services
builder.Services.AddScoped<IEventService, EventService>();

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/", () => Results.Ok("Hello World!"));

app.Run();
