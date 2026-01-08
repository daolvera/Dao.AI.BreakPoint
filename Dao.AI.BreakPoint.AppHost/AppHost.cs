var builder = DistributedApplication.CreateBuilder(args);

// Application Insights for telemetry
var insights = builder.AddAzureApplicationInsights("appinsights");

// Key Vault for secrets
var keyVault = builder.AddAzureKeyVault("keyvault");

// Database
var postgres = builder.AddPostgres("postgres").WithLifetime(ContainerLifetime.Persistent);

var breakPointDb = postgres.AddDatabase("breakpointdb");

// Azure Storage (Azurite for local dev)
var storage = builder
    .AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithLifetime(ContainerLifetime.Persistent));

var blobStorage = storage.AddBlobs("blobstorage");

// Migrations
var migrations = builder
    .AddProject<Projects.Dao_AI_BreakPoint_Migrations>("breakpointmigrations")
    .WithReference(breakPointDb)
    .WaitFor(breakPointDb);

// API Service
var breakPointApi = builder
    .AddProject<Projects.Dao_AI_BreakPoint_ApiService>("breakpointapi")
    .WaitFor(migrations)
    .WithReference(breakPointDb)
    .WithReference(blobStorage)
    .WithReference(insights)
    .WithReference(keyVault)
    .WithHttpHealthCheck("/health");

// Azure Function (Swing Analyzer)
var analyzerFunction = builder
    .AddAzureFunctionsProject<Projects.Dao_AI_BreakPoint_AnalyzerFunction>(
        "breakpointanalyzerfunction"
    )
    .WithHostStorage(storage)
    .WithReference(breakPointDb)
    .WithReference(blobStorage)
    .WithReference(insights)
    .WithReference(keyVault);

// Frontend App
var breakPointApp = builder
    .AddJavaScriptApp("breakpoint", "../Dao.AI.BreakPoint.Web", "start")
    .WithReference(breakPointApi)
    .WaitFor(breakPointApi)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithNpm(installCommand: "ci")
    .PublishAsDockerFile();

var frontendHttpEndpoint = breakPointApp.GetEndpoint("http");

breakPointApi.WithEnvironment("BreakPointAppUrl", frontendHttpEndpoint);

builder.Build().Run();
