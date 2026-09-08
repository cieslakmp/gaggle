using System.Reflection;

namespace Gaggle;

/// <summary>
/// Who and what this is, for the tray tooltip and the About box.
///
/// Everything here comes from the assembly rather than being retyped, so the version
/// a tester reports is by construction the one the release workflow stamped.
/// </summary>
public static class AppInfo
{
    public const string Name = "Gaggle";

    public const string Author = "Maciej Cieslak - VLZ";

    public const string RepositoryUrl = "https://github.com/cieslakmp/gaggle";

    /// <summary>
    /// The version as a person would write it — "0.3.0", not "0.3.0.0".
    ///
    /// Read from the informational version, which is what <c>&lt;Version&gt;</c>
    /// produces, with the build metadata the SDK appends after a '+' stripped. That
    /// suffix is a commit hash: meaningless in a tooltip, and expensive in one, since
    /// <see cref="System.Windows.Forms.NotifyIcon.Text"/> allows 63 characters.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// One line on what the app is, taken from the csproj description. Written down
    /// once, there: the SDK emits the attribute from it on every build, and
    /// AppInfoTests is what says so out loud if that ever stops being true.
    /// </summary>
    public static string Description { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
            ?? string.Empty;

    private static string ReadVersion()
    {
        string? informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            // AssemblyVersion is always present but pads to four parts.
            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        }

        int metadata = informational.IndexOf('+', StringComparison.Ordinal);

        return metadata < 0 ? informational : informational[..metadata];
    }
}
