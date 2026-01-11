using Dao.AI.BreakPoint.ApiService.Configuration;
using Dao.AI.BreakPoint.ApiService.Hubs;
using Dao.AI.BreakPoint.ApiService.Services;
using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for reverse proxy (Azure Container Apps)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear known networks/proxies to trust all proxies (needed for Azure Container Apps)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Add Azure Key Vault secrets as configuration provider (Aspire extension)
builder.Configuration.AddAzureKeyVaultSecrets("keyvault");

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
builder.Services.AddAzureOpenAIServices(builder.Configuration);

// Configure Azure Blob Storage using Aspire extension
builder.AddAzureBlobServiceClient("BlobStorage");
builder.Services.AddAspirerBlobStorage();

// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IAnalysisNotificationService, AnalysisNotificationService>();

builder.AddNpgsqlDbContext<BreakPointDbContext>("BreakPointDb");

builder.AddBreakPointAuthenticationAndAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Use forwarded headers - must be first middleware
app.UseForwardedHeaders();

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

// Database migrations are handled by the Migrations project
// Do not use EnsureCreatedAsync() as it bypasses migrations

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
