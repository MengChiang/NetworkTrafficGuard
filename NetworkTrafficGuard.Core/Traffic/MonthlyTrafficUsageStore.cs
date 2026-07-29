using System.Text.Json;

namespace NetworkTrafficGuard.Core.Traffic;

public sealed class MonthlyTrafficUsageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _syncRoot = new();
    private readonly string _filePath;

    public MonthlyTrafficUsageStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? GetDefaultFilePath()
            : filePath;
    }

    public MonthlyTrafficUsageEntry RecordSample(
        string routeKey,
        int interfaceIndex,
        string gateway,
        string displayName,
        long bytesReceived,
        long bytesSent,
        DateTimeOffset sampledAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);

        lock (_syncRoot)
        {
            var document = LoadDocument();
            var month = sampledAt.ToString("yyyy-MM");
            var entryKey = CreateEntryKey(month, routeKey);

            if (!document.Entries.TryGetValue(entryKey, out var entry))
            {
                entry = new MonthlyTrafficUsageEntry
                {
                    Month = month,
                    RouteKey = routeKey,
                    InterfaceIndex = interfaceIndex,
                    Gateway = gateway,
                    DisplayName = displayName,
                    LastBytesReceived = bytesReceived,
                    LastBytesSent = bytesSent,
                    UpdatedAt = sampledAt
                };
                document.Entries[entryKey] = entry;
            }
            else
            {
                entry.InterfaceIndex = interfaceIndex;
                entry.Gateway = gateway;
                entry.DisplayName = displayName;
                entry.BytesReceived += CalculateDelta(entry.LastBytesReceived, bytesReceived);
                entry.BytesSent += CalculateDelta(entry.LastBytesSent, bytesSent);
                entry.LastBytesReceived = bytesReceived;
                entry.LastBytesSent = bytesSent;
                entry.UpdatedAt = sampledAt;
            }

            SaveDocument(document);
            return entry;
        }
    }

    public IReadOnlyList<MonthlyTrafficUsageEntry> GetEntries(string? month = null)
    {
        lock (_syncRoot)
        {
            var targetMonth = string.IsNullOrWhiteSpace(month)
                ? DateTimeOffset.Now.ToString("yyyy-MM")
                : month;

            return LoadDocument()
                .Entries
                .Values
                .Where(entry => string.Equals(entry.Month, targetMonth, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.TotalBytes)
                .ToList();
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var scaled = (double)value;

        while (scaled >= 1024 && unitIndex < units.Length - 1)
        {
            scaled /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value} {units[unitIndex]}"
            : $"{scaled:0.0} {units[unitIndex]}";
    }

    private static long CalculateDelta(long previous, long current)
    {
        return current >= previous
            ? current - previous
            : 0;
    }

    private static string CreateEntryKey(string month, string routeKey)
    {
        return $"{month}|{routeKey}";
    }

    private static string GetDefaultFilePath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "NetworkTrafficGuard", "traffic-usage.json");
    }

    private TrafficUsageDocument LoadDocument()
    {
        if (!File.Exists(_filePath))
        {
            return new TrafficUsageDocument();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<TrafficUsageDocument>(json, JsonOptions)
                ?? new TrafficUsageDocument();
        }
        catch (JsonException)
        {
            return new TrafficUsageDocument();
        }
        catch (IOException)
        {
            return new TrafficUsageDocument();
        }
    }

    private void SaveDocument(TrafficUsageDocument document)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, JsonOptions);
        var tempPath = $"{_filePath}.tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(_filePath))
        {
            File.Replace(tempPath, _filePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _filePath);
        }
    }

    private sealed class TrafficUsageDocument
    {
        public Dictionary<string, MonthlyTrafficUsageEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
