using Dao.AI.BreakPoint.Data;
using Dao.AI.BreakPoint.Migrations;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<BreakPointDbContext>("BreakPointDb");

var host = builder.Build();
host.Run();
