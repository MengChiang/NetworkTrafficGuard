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
        var settingsPath = GetSettingsPath();

        if (!File.Exists(settingsPath))
        {
            return new NetworkGuardSettings();
        }

        var json = File.ReadAllText(settingsPath);
        return JsonSerializer.Deserialize<NetworkGuardSettings>(json, JsonOptions)
            ?? new NetworkGuardSettings();
    }

    public static void Save(NetworkGuardSettings settings)
    {
        var settingsPath = GetSettingsPath();
        var writeOptions = new JsonSerializerOptions(JsonOptions)
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(settings, writeOptions);
        File.WriteAllText(settingsPath, json);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }
}
