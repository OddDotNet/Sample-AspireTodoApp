using Microsoft.EntityFrameworkCore;
using TodoServiceDefaults;
using TodoWebApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations(true, false);
});
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseSqlite("Data Source=sqlite.db");
});

// Add service defaults to set up OpenTelemetry. See https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults.
builder.AddServiceDefaults();

var app = builder.Build();

// Wipe out the database and recreate between runs.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<TodoDbContext>();
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();