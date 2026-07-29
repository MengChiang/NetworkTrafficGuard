using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetworkTrafficGuard.Core.Settings;

namespace NetworkTrafficGuard.Tray.Settings;

public static class TraySettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static NetworkGuardSettings Load()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(settingsPath))
        {
            return new NetworkGuardSettings();
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<NetworkGuardSettings>(json, JsonOptions)
            ?? new NetworkGuardSettings();
    }
}
