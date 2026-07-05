using System.Reflection;

namespace QuickWindowScreenshot;

internal static class AppVersion
{
    public static string DisplayText => $"v{InformationalVersion()}";

    private static string InformationalVersion()
    {
        Assembly assembly = typeof(AppVersion).Assembly;
        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(version))
        {
            int metadataStart = version.IndexOf('+', StringComparison.Ordinal);
            return metadataStart > 0 ? version[..metadataStart] : version;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
