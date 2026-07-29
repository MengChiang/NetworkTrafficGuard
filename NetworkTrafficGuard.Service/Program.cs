using NetworkTrafficGuard.Service;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Service.Diagnostics;
using NetworkTrafficGuard.Windows;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<NetworkGuardSettings>(builder.Configuration);
builder.Services.AddSingleton<IRouteReader, PowerShellRouteReader>();
builder.Services.AddSingleton<IRouteController, PowerShellRouteController>();
builder.Services.AddSingleton<INetworkPolicyEngine, NetworkPolicyEngine>();
builder.Services.AddSingleton<RouteDiagnosticsLogger>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
