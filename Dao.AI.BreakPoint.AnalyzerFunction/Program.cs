using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Add local.settings.json for local development
builder
    .Configuration.SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add Azure Key Vault secrets as configuration provider (Aspire extension)
builder.Configuration.AddAzureKeyVaultSecrets("keyvault");

builder.ConfigureFunctionsWebApplication();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Configure database connection using Aspire PostgreSQL extension
builder.AddNpgsqlDbContext<BreakPointDbContext>("BreakPointDb");

// Configure Azure Blob Storage using Aspire extension
builder.AddAzureBlobServiceClient("BlobStorage");

// Register video processing service
builder.Services.AddSingleton<IVideoProcessingService, OpenCvVideoProcessingService>();

builder.Services.AddAspirerBlobStorage();

// Register services with configuration-based IOptions
builder.Services.AddAzureOpenAIServices(builder.Configuration);
builder.Services.AddSwingAnalyzerServices(builder.Configuration);
builder.Services.AddAnalysisServices();
builder.Services.AddBreakPointServices();

// Register analysis notification client for SignalR notifications
var apiBaseUrl = builder.Configuration["services:breakpointapi:https:0"]
    ?? builder.Configuration["services:breakpointapi:http:0"]
    ?? builder.Configuration["BreakPointApiUrl"]
    ?? throw new InvalidOperationException(
        "BreakPointApiUrl configuration is required for analysis notifications"
    );
builder.Services.AddAnalysisNotificationClient(apiBaseUrl);

builder.Build().Run();
