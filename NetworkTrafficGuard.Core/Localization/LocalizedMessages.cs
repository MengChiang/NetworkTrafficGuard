using System.Globalization;
using System.Reflection;
using System.Resources;

namespace NetworkTrafficGuard.Core.Localization;

public static class LocalizedMessages
{
    private static readonly ResourceManager ResourceManager = new(
        "NetworkTrafficGuard.Core.Resources.PolicyMessages",
        Assembly.GetExecutingAssembly());

    public static string Get(string key, string? cultureName = null)
    {
        var culture = SupportedCultures.GetCultureOrDefault(cultureName);
        return ResourceManager.GetString(key, culture) ?? key;
    }
}
