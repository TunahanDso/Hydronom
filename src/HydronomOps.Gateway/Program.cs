using HydronomOps.Gateway.BackgroundJobs;
using HydronomOps.Gateway.Configuration;
using HydronomOps.Gateway.Endpoints;
using HydronomOps.Gateway.Infrastructure.Broadcast;
using HydronomOps.Gateway.Infrastructure.TcpIngress;
using HydronomOps.Gateway.Infrastructure.Time;
using HydronomOps.Gateway.Services.Health;
using HydronomOps.Gateway.Services.Mapping;
using HydronomOps.Gateway.Services.RuntimeOps;
using HydronomOps.Gateway.Services.State;

var builder = WebApplication.CreateBuilder(args);

const string HydronomOpsCorsPolicy = "HydronomOpsCorsPolicy";

builder.Services.Configure<GatewayOptions>(
    builder.Configuration.GetSection(GatewayOptions.SectionName));

builder.Services.Configure<RuntimeTcpOptions>(
    builder.Configuration.GetSection(RuntimeTcpOptions.SectionName));

builder.Services.Configure<HydronomOps.Gateway.Configuration.WebSocketOptions>(
    builder.Configuration.GetSection(HydronomOps.Gateway.Configuration.WebSocketOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy(HydronomOpsCorsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<ISystemClock>(_ => new SystemClock());
builder.Services.AddSingleton<IGatewayStateStore, GatewayStateStore>();
builder.Services.AddSingleton<RuntimeToGatewayMapper>();
builder.Services.AddSingleton<IGatewayHealthService, GatewayHealthService>();
builder.Services.AddSingleton<IGatewayRuntimeOpsProjectionService, GatewayRuntimeOpsProjectionService>();

builder.Services.AddSingleton<GatewayWebSocketConnectionManager>();
builder.Services.AddSingleton<GatewayBroadcastService>();

builder.Services.AddSingleton<RuntimeFrameParser>();
builder.Services.AddSingleton<RuntimeTcpClientService>();
builder.Services.AddHostedService<RuntimeIngressHostedService>();

builder.Services.AddHostedService<HeartbeatBroadcastWorker>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseCors(HydronomOpsCorsPolicy);

app.UseWebSockets();

app.MapGet("/", () => Results.Ok(new
{
    service = "HydronomOps.Gateway",
    status = "running",
    utc = DateTime.UtcNow
}));

app.MapStatusEndpoints();
app.MapSnapshotEndpoints();
app.MapRuntimeOpsEndpoints();
app.MapWebSocketEndpoints();

app.Run();