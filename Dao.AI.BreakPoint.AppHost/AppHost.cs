var builder = DistributedApplication.CreateBuilder(args);

var mysql = builder.AddMySql("mysql")
    .WithLifetime(ContainerLifetime.Persistent);

var breakpointdb = mysql.AddDatabase("BreakPointDb");

var apiService = builder.AddProject<Projects.Dao_AI_BreakPoint_ApiService>("apiservice")
    .WithReference(breakpointdb)
    .WithHttpHealthCheck("/health");

var webApp = builder.AddNpmApp("webapp", "../Dao.AI.BreakPoint.Web")
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
