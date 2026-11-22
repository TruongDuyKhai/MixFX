using System.Reflection;

namespace Soundboard.App.Services;

/// <summary>Build information shown to the user and written to the log.</summary>
public static class AppInfo
{
    private const string UnknownVersion = "unknown";

    /// <summary>SemVer from Directory.Build.props, e.g. "0.1.0".</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // The SDK appends "+<commit sha>"; the user only needs the SemVer part.
        return informational?.Split('+')[0] ?? UnknownVersion;
    }
}
