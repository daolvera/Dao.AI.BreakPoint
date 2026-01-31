using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Container App Environment to enable role assignments
builder.AddAzureContainerAppEnvironment("breakpointenv");

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
    .WithExternalHttpEndpoints() // Required for OAuth callbacks
    .WithRoleAssignments(
        storage,
        StorageBuiltInRole.StorageBlobDataContributor,
        StorageBuiltInRole.StorageBlobDataOwner
    )
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
    .WithReference(keyVault)
    .WithReference(breakPointApi)
    .WithRoleAssignments(
        storage,
        StorageBuiltInRole.StorageBlobDataContributor,
        StorageBuiltInRole.StorageBlobDataReader
    )
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .PublishAsDockerFile()
    .WithHttpHealthCheck("/health");

// Frontend App
var breakPointApp = builder
    .AddJavaScriptApp("breakpoint", "../Dao.AI.BreakPoint.Web", "start")
    .WithReference(breakPointApi)
    .WaitFor(breakPointApi)
    .WithHttpEndpoint(targetPort: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithNpm(installCommand: "ci")
    .PublishAsDockerFile()
    .WithHttpHealthCheck("/health");

if (!string.IsNullOrWhiteSpace(builder.Configuration["BreakPointBaseUrl"]))
{
    breakPointApi.WithEnvironment("BreakPointAppUrl", builder.Configuration["BreakPointBaseUrl"]);
}
else
{
    var frontendHttpEndpoint = breakPointApp.GetEndpoint("http");
    breakPointApi.WithEnvironment("BreakPointAppUrl", frontendHttpEndpoint);
}

builder.Build().Run();
