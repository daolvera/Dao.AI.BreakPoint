using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Services;
using Dao.AI.BreakPoint.Services.Options;
using Dao.AI.BreakPoint.Services.VideoProcessing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

// Configure database connection using Aspire PostgreSQL extension
builder.AddNpgsqlDbContext<BreakPointDbContext>("BreakPointDb");

// Configure Azure Blob Storage using Aspire extension
builder.AddAzureBlobServiceClient("BlobStorage");

// Register video processing service
builder.Services.AddSingleton<IVideoProcessingService, OpenCvVideoProcessingService>();

// Configure MoveNet options
builder.Services.Configure<MoveNetOptions>(options =>
{
    var modelPath = builder.Configuration["MoveNet:ModelPath"];
    options.ModelPath = modelPath ?? "movenet/movenet_singlepose_thunder.onnx";
});

// Configure Swing Quality Model options
builder.Services.Configure<SwingQualityModelOptions>(options =>
{
    var section = builder.Configuration.GetSection(SwingQualityModelOptions.SectionName);
    if (section.Exists())
    {
        section.Bind(options);
    }
});

// Register blob storage using Aspire-injected BlobServiceClient
builder.Services.AddAspirerBlobStorage();

// Register analysis services (includes repositories)
builder.Services.AddAnalysisServices();

// Register swing analyzer services (includes SkeletonOverlayService)
builder.Services.AddSwingAnalyzerServices();

// Register base BreakPoint services
builder.Services.AddBreakPointServices();

builder.Build().Run();
