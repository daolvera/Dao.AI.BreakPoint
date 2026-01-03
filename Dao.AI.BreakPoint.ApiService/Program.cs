using Dao.AI.BreakPoint.ApiService.Configuration;
using Dao.AI.BreakPoint.ApiService.Hubs;
using Dao.AI.BreakPoint.ApiService.Services;
using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Services;
using Microsoft.IdentityModel.Protocols.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAngularApp",
        policy =>
        {
            policy
                .WithOrigins(
                    builder.Configuration["BreakPointAppUrl"]
                        ?? throw new InvalidConfigurationException(
                            "BreakPointAppUrl is not configured"
                        )
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials(); // Required for SignalR
        }
    );
});

builder.Services.AddControllers();
builder.Services.AddBreakPointServices();
builder.Services.AddBreakPointIdentityServices();
builder.Services.AddAnalysisServices();

// Configure Azure Blob Storage using Aspire extension
builder.AddAzureBlobServiceClient("BlobStorage");
builder.Services.AddAspirerBlobStorage();

// Add coaching service - uses Azure OpenAI if configured, otherwise static tips
var azureOpenAISection = builder.Configuration.GetSection("AzureOpenAI");
if (azureOpenAISection.Exists() && !string.IsNullOrEmpty(azureOpenAISection["Endpoint"]))
{
    builder.Services.AddCoachingService(options => azureOpenAISection.Bind(options));
}
else
{
    builder.Services.AddCoachingService(); // Falls back to static tips
}

// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IAnalysisNotificationService, AnalysisNotificationService>();

builder.AddNpgsqlDbContext<BreakPointDbContext>("BreakPointDb");

builder.AddBreakPointAuthenticationAndAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

app.UseCors("AllowAngularApp");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BreakPoint API V1");
        c.RoutePrefix = string.Empty; // This makes Swagger UI available at the app's root
    });
}

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BreakPointDbContext>();
    await context.Database.EnsureCreatedAsync();
    //if (app.Environment.IsDevelopment())
    //{
    //    await Seeder.SeedFakeData(context);
    //}
}

app.MapControllers();

// Map SignalR hub
app.MapHub<AnalysisHub>("/hubs/analysis");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .WithSummary("Check API health");

app.MapDefaultEndpoints();

app.Run();
