using Dao.AI.BreakPoint.ApiService.Endpoints;
using Dao.AI.BreakPoint.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add DbContext with MySQL
builder.AddMySqlDbContext<BreakPointDbContext>("BreakPointDb");

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Use CORS
app.UseCors("AllowAngularApp");

// Add Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BreakPointDbContext>();
    context.Database.EnsureCreated();
    if (app.Environment.IsDevelopment())
    {
        await Seeder.Seed(context);
    }
}

// Map endpoints from separate files
app.MapPlayerEndpoints();
app.MapMatchEndpoints();
app.MapSwingAnalysisEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithSummary("Check API health");

app.MapDefaultEndpoints();

app.Run();
