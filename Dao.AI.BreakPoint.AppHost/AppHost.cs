var builder = DistributedApplication.CreateBuilder(args);

var mysql = builder.AddMySql("mysql")
    .WithLifetime(ContainerLifetime.Persistent);

var breakPointDb = mysql.AddDatabase("BreakPointDb");

var breakPointApi = builder.AddProject<Projects.Dao_AI_BreakPoint_ApiService>("breakPointApi")
    .WithReference(breakPointDb)
    .WaitFor(breakPointDb)
    .WithHttpHealthCheck("/health");

builder.AddNpmApp("webapp", "../Dao.AI.BreakPoint.Web")
    .WithReference(breakPointApi)
    .WaitFor(breakPointApi)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
