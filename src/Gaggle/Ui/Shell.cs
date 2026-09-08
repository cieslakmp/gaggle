using System.Diagnostics;

namespace Gaggle.Ui;

/// <summary>
/// Hands a link or a folder to Windows, on purpose and nothing else.
///
/// <see cref="ProcessStartInfo.UseShellExecute"/> is what makes a link open in the
/// browser, and it is also what makes this worth being careful about: the shell
/// dispatches by scheme, so a string that is not the http(s) URL it looks like is a
/// program launch rather than a page. Most callers here pass a compile-time constant and
/// could not care less, but one does not — a release's <c>html_url</c> arrives from the
/// GitHub API, and the app should not be the thing that turns a bad API response into a
/// started process.
///
/// So the check lives here rather than at that one call site, because the next remote
/// string someone opens will not come with a reminder.
/// </summary>
// Public for the same reason ModelInstaller and UpdateInstaller are: the test project
// checks IsWebUrl, and that check is the whole point of this class.
public static class Shell
{
    /// <summary>
    /// True for the only two schemes a link in this app is ever meant to have. Separate
    /// from <see cref="OpenUrl"/> so it can be tested without starting anything.
    /// </summary>
    public static bool IsWebUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Opens a web link. A URL that is not http(s) is ignored rather than handed to the
    /// shell — there is nothing useful to do with it and nothing worth risking.
    /// </summary>
    public static void OpenUrl(string? url)
    {
        if (!IsWebUrl(url))
        {
            return;
        }

        Start(url!);
    }

    /// <summary>
    /// Opens a local file or folder — the config file and the data directory, both of
    /// which this app owns and neither of which comes from anywhere else.
    /// </summary>
    public static void OpenPath(string path) => Start(path);

    /// <summary>
    /// A dead link, a missing folder or no handler at all should not take the tray icon
    /// down, so the failure is swallowed. There is nothing the user could do with it.
    /// </summary>
    private static void Start(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
        }
    }
}
