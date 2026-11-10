using Microsoft.Win32;

namespace Gaggle.Configuration;

/// <summary>
/// "Start with Windows", held where Windows keeps that decision rather than in
/// config.json: the per-user <c>Run</c> key.
///
/// One copy of the answer on purpose. A bool in the config would be a second place to
/// disagree with the registry — a pilot who clears the value with an autorun tool, or
/// copies a config between machines, would get a ticked menu item and no autostart, with
/// nothing anywhere to say which one was lying. Reading the key back is cheap enough to
/// do every time the menu opens.
///
/// HKCU, not HKLM: per-user, no elevation, and there is nothing for Gaggle to do before
/// somebody has logged in.
/// </summary>
// Public for the same reason Shell and UpdateInstaller are: the test project pins the
// quoting, which is the part that fails silently.
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "Gaggle";

    /// <summary>
    /// The command Windows runs at logon.
    ///
    /// Quoted because the install folder is wherever the pilot unzipped it, and
    /// <c>C:\Program Files\Gaggle\Gaggle.exe</c> unquoted asks Windows to run
    /// <c>C:\Program</c> — which fails at logon, where nobody is watching, and nowhere
    /// else. Separate from the rest so it can be tested without touching the registry.
    /// </summary>
    public static string CommandFor(string exePath) => $"\"{exePath}\"";

    /// <summary>
    /// <see cref="Environment.ProcessPath"/> rather than <c>Assembly.Location</c>, which
    /// is *empty* under PublishSingleFile — the same trap <c>UpdateInstaller</c> avoids.
    /// </summary>
    private static string Command => CommandFor(Environment.ProcessPath!);

    /// <summary>True when Windows has been asked to start Gaggle at logon.</summary>
    public static bool IsEnabled => Read() is not null;

    /// <summary>
    /// Adds or removes the registration. A refused write is swallowed, which is honest
    /// rather than lazy: the menu reads <see cref="IsEnabled"/> every time it opens, so
    /// a check mark that will not stick is exactly what happened.
    /// </summary>
    public static void Set(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                key.SetValue(ValueName, Command);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
        }
    }

    private static string? Read()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);

        return key?.GetValue(ValueName) as string;
    }
}
