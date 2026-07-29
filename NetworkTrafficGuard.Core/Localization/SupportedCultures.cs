using System.Globalization;

namespace NetworkTrafficGuard.Core.Localization;

public static class SupportedCultures
{
    public const string English = "en-US";
    public const string TraditionalChinese = "zh-TW";
    public const string SimplifiedChinese = "zh-CN";
    public const string Japanese = "ja-JP";

    public const string DefaultCultureName = TraditionalChinese;

    public static readonly IReadOnlySet<string> CultureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        English,
        TraditionalChinese,
        SimplifiedChinese,
        Japanese
    };

    public static CultureInfo GetCultureOrDefault(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.GetCultureInfo(DefaultCultureName);
        }

        var normalizedCultureName = NormalizeAlias(cultureName);

        return CultureNames.Contains(normalizedCultureName)
            ? CultureInfo.GetCultureInfo(normalizedCultureName)
            : CultureInfo.GetCultureInfo(DefaultCultureName);
    }

    private static string NormalizeAlias(string cultureName)
    {
        return cultureName.Trim().ToLowerInvariant() switch
        {
            "en" => English,
            "zh-tw" => TraditionalChinese,
            "zh-hant" => TraditionalChinese,
            "zh-cn" => SimplifiedChinese,
            "zh-hans" => SimplifiedChinese,
            "jp" => Japanese,
            "ja" => Japanese,
            "ja-jp" => Japanese,
            _ => cultureName.Trim()
        };
    }
}
