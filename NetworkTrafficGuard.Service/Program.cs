using NetworkTrafficGuard.Service;
using NetworkTrafficGuard.Core.Policy;
using NetworkTrafficGuard.Core.Routes;
using NetworkTrafficGuard.Core.Settings;
using NetworkTrafficGuard.Service.Windows;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<NetworkGuardSettings>(builder.Configuration);
builder.Services.AddSingleton<IRouteReader, PowerShellRouteReader>();
builder.Services.AddSingleton<INetworkPolicyEngine, NetworkPolicyEngine>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
