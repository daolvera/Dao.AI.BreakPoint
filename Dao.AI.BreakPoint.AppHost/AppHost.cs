var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Dao_AI_BreakPoint_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddMySql("BreakPointDb")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Dao_AI_BreakPoint_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
