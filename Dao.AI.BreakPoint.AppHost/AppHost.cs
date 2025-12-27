var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

var breakPointDb = postgres.AddDatabase("BreakPointDb");

var migrations = builder.AddProject<Projects.Dao_AI_BreakPoint_Migrations>("BreakPointMigrations")
    .WithReference(breakPointDb)
    .WaitFor(breakPointDb);

var breakPointApi = builder.AddProject<Projects.Dao_AI_BreakPoint_ApiService>("BreakPointApi")
    .WaitFor(migrations)
    .WithReference(breakPointDb)
    .WithHttpHealthCheck("/health");

var breakPointApp = builder.AddJavaScriptApp("BreakPoint", "../Dao.AI.BreakPoint.Web", "start")
    .WithReference(breakPointApi)
    .WaitFor(breakPointApi)
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithNpm(installCommand: "ci")
    .PublishAsDockerFile();

var frontendHttpEndpoint = breakPointApp.GetEndpoint("http");

breakPointApi.WithEnvironment("BreakPointAppUrl", frontendHttpEndpoint);

builder.Build().Run();
