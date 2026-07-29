using NetworkTrafficGuard.Service;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Core.Traffic;
using NetworkTrafficGuard.Service.Diagnostics;
using NetworkTrafficGuard.Windows;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Network Traffic Guard";
});

builder.Services.Configure<NetworkGuardSettings>(builder.Configuration);
builder.Services.AddSingleton<IRouteReader, NativeIpHelperRouteReader>();
builder.Services.AddSingleton<IRouteController, PowerShellRouteController>();
builder.Services.AddSingleton<INetworkPolicyEngine, NetworkPolicyEngine>();
builder.Services.AddSingleton<MonthlyTrafficUsageStore>();
builder.Services.AddSingleton<RouteDiagnosticsLogger>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
