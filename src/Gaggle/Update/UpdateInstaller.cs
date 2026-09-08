using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Gaggle.Configuration;
using Gaggle.Localisation;
using Gaggle.Net;

namespace Gaggle.Update;

/// <summary>
/// Replaces the installed copy of Gaggle with a newer release.
///
/// A running executable cannot overwrite itself, and Gaggle runs asInvoker with no way to
/// elevate, so the swap is done by a small script that waits for this process to exit and
/// then copies the new files over. Everything before that point is reversible: the
/// download and the extracted files live under %APPDATA%, and nothing in the install
/// folder is touched until the package has been verified.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>The file name the release ships. A renamed copy is not updated in place.</summary>
    public const string ExecutableName = "Gaggle.exe";

    /// <summary>
    /// Whisper's native library, checked after extraction for the same reason release.yml
    /// checks it before packaging: an app missing it starts, looks fine, and cannot
    /// transcribe a word.
    /// </summary>
    private static readonly string NativeLibraryPath = Path.Combine("runtimes", "win-x64", "whisper.dll");

    private static readonly string UpdatesDirectory = Path.Combine(AppConfig.DataDirectory, "updates");

    private static readonly string StagingDirectory = Path.Combine(UpdatesDirectory, "staging");

    private static readonly string ScriptPath = Path.Combine(UpdatesDirectory, "apply.cmd");

    private static readonly char[] ChecksumSeparators = [' ', '\t'];

    /// <summary>
    /// The most an update package may be. The real one is around 50 MB, so this is room
    /// to grow rather than a tight fit — it exists to bound a download that would
    /// otherwise run until the disk filled, because the checksum cannot be verified until
    /// the whole file has landed.
    /// </summary>
    private const long MaxPackageBytes = 500L * 1024 * 1024;

    /// <summary>The name to fall back to when the release does not supply a usable one.</summary>
    private const string DefaultPackageName = "Gaggle-update.zip";

    /// <summary>
    /// The folder holding the running Gaggle.exe, or null when this is not an installed
    /// copy — a dev run through dotnet, or a renamed executable, either of which would
    /// end up with two copies side by side if updated in place.
    ///
    /// Read from <see cref="Environment.ProcessPath"/> and never from Assembly.Location,
    /// which is empty in a single-file publish.
    /// </summary>
    public static string? InstallDirectory { get; } = ResolveInstallDirectory();

    /// <summary>
    /// Whether the install folder can actually be written to. Program Files, a read-only
    /// share and a locked-down folder all fail here, and the caller then offers the
    /// releases page rather than starting something it cannot finish.
    /// </summary>
    public static bool CanInstallInPlace()
    {
        string? directory = InstallDirectory;

        if (directory is null)
        {
            return false;
        }

        string probe = Path.Combine(directory, $".gaggle-update-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a SHA256 out of a checksum file, accepting both a bare hash and the
    /// "hash  filename" form that sha256sum and Get-FileHash pipelines produce. Returns
    /// null for anything that is not a 64-character hex digest, so a truncated response
    /// or an HTML error page can never be mistaken for a match.
    /// </summary>
    public static string? ParseChecksum(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (string line in text.Split('\n'))
        {
            string candidate = line.Trim();

            int separator = candidate.IndexOfAny(ChecksumSeparators);
            if (separator > 0)
            {
                candidate = candidate[..separator];
            }

            if (candidate.Length == 64 && candidate.All(Uri.IsHexDigit))
            {
                return candidate.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// The file name to save the package under.
    /// <see cref="ReleaseInfo.PackageName"/> is whatever GitHub called the asset, so it is
    /// treated as untrusted text rather than as a file name: <see cref="Path.Combine"/>
    /// hands back a rooted string unchanged, and follows <c>..\</c> straight out of the
    /// updates folder — into a Startup folder, given a name chosen for it. Only the leaf
    /// survives, and only if it is a name a file can actually have.
    /// </summary>
    public static string PackageFileName(string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return DefaultPackageName;
        }

        string name = Path.GetFileName(supplied.Trim());

        // GetFileName leaves "." and ".." alone, and Path.Combine reads both as a
        // directory rather than as the file this is supposed to name.
        if (name.Length == 0
            || name == "."
            || name == ".."
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return DefaultPackageName;
        }

        return name;
    }

    /// <summary>
    /// Downloads, verifies and stages a release, then launches the script that swaps it
    /// in. Returns once that script is running; the caller must exit promptly, because
    /// the script is waiting on this process to let go of its own executable.
    ///
    /// Throws <see cref="InvalidOperationException"/> with a message meant for the user
    /// when the release cannot be installed. Nothing in the install folder has been
    /// touched at that point.
    /// </summary>
    public static async Task InstallAsync(
        ReleaseInfo release,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string? installDirectory = InstallDirectory;

        if (installDirectory is null)
        {
            throw new InvalidOperationException(Strings.Current.NotANormalInstall);
        }

        if (release.PackageUrl is null || release.ChecksumUrl is null)
        {
            throw new InvalidOperationException(Strings.Current.NoVerifiablePackage);
        }

        Directory.CreateDirectory(UpdatesDirectory);

        string packagePath = Path.Combine(UpdatesDirectory, PackageFileName(release.PackageName));

        // Written by FileStream rather than by a browser, so the file carries no Mark of
        // the Web and the executable extracted from it does not re-trigger the SmartScreen
        // warning the README documents for a manual download.
        await FileDownloader.DownloadAsync(
            release.PackageUrl,
            packagePath,
            MaxPackageBytes,
            progress,
            cancellationToken);

        try
        {
            await VerifyAsync(packagePath, release.ChecksumUrl, cancellationToken);
            Extract(packagePath);
        }
        catch
        {
            // A package that failed verification is not left behind to be retried by
            // anything, or mistaken for a good one later.
            FileDownloader.TryDelete(packagePath);
            throw;
        }

        // Normalised rather than trusted to the source file: cmd.exe mis-parses labels
        // and goto in a batch file with bare LF endings, and .gitattributes decides how
        // this repository is checked out.
        File.WriteAllText(ScriptPath, BuildScript().ReplaceLineEndings("\r\n"), Encoding.ASCII);
        LaunchScript(installDirectory);
    }

    private static async Task VerifyAsync(
        string packagePath,
        string checksumUrl,
        CancellationToken cancellationToken)
    {
        string? expected;

        try
        {
            string text = await FileDownloader.GetStringAsync(
                checksumUrl,
                TimeSpan.FromSeconds(30),
                headers: null,
                cancellationToken);

            expected = ParseChecksum(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(Strings.Current.CouldNotFetchChecksum, ex);
        }

        if (expected is null)
        {
            throw new InvalidOperationException(Strings.Current.ChecksumUnreadable);
        }

        string actual = await FileDownloader.ComputeSha256Async(packagePath, cancellationToken);

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Strings.Current.ChecksumMismatch);
        }
    }

    private static void Extract(string packagePath)
    {
        if (Directory.Exists(StagingDirectory))
        {
            Directory.Delete(StagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(StagingDirectory);
        ZipFile.ExtractToDirectory(packagePath, StagingDirectory, overwriteFiles: true);

        if (!File.Exists(Path.Combine(StagingDirectory, ExecutableName))
            || !File.Exists(Path.Combine(StagingDirectory, NativeLibraryPath)))
        {
            Directory.Delete(StagingDirectory, recursive: true);

            throw new InvalidOperationException(Strings.Current.PackageMissingFiles);
        }

        // The zip is only a delivery format, and keeping it costs ~80 MB per update.
        FileDownloader.TryDelete(packagePath);
    }

    private static void LaunchScript(string installDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,

            // Neither the install folder nor the staging folder, so the script never holds
            // open a directory it is about to write to or delete.
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
        };

        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(ScriptPath);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add(installDirectory);
        startInfo.ArgumentList.Add(StagingDirectory);

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(Strings.Current.CouldNotStartUpdater);
        }
    }

    private static string? ResolveInstallDirectory()
    {
        string? processPath = Environment.ProcessPath;

        if (processPath is null
            || !string.Equals(Path.GetFileName(processPath), ExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetDirectoryName(processPath);
    }

    /// <summary>
    /// The swap itself, as a batch file.
    ///
    /// cmd rather than PowerShell because an AllSigned execution policy can refuse a
    /// script even with -ExecutionPolicy Bypass, and cmd rather than a helper executable
    /// because a second exe would have to be added to the release package and asserted
    /// for in two workflows. The wait uses ping rather than timeout: timeout needs a
    /// console, and this runs without one.
    /// </summary>
    private static string BuildScript() =>
        """
        @echo off
        setlocal
        set "GAGGLEPID=%~1"
        set "TARGET=%~2"
        set "STAGING=%~3"

        rem Wait for Gaggle to exit; a running executable cannot be overwritten. If it
        rem never goes away, do nothing at all rather than force the issue - a failed
        rem update must leave a working install behind.
        set /a TRIES=0

        :wait
        tasklist /FI "PID eq %GAGGLEPID%" /NH 2>nul | find "%GAGGLEPID%" >nul
        if errorlevel 1 goto ready
        set /a TRIES+=1
        if %TRIES% GEQ 30 goto done
        ping -n 2 127.0.0.1 >nul
        goto wait

        :ready
        rem No /PURGE: the install folder may hold files the user put there, and tidying
        rem up stale runtimes is not worth deleting them for. Robocopy treats any exit
        rem code below 8 as success.
        robocopy "%STAGING%" "%TARGET%" /E /IS /IT /R:3 /W:1 /NFL /NDL /NJH /NJS >nul
        if %ERRORLEVEL% GEQ 8 goto done

        rmdir /s /q "%STAGING%" 2>nul
        start "" "%TARGET%\Gaggle.exe"

        :done
        endlocal
        rem Delete this script on the way out.
        (goto) 2>nul & del "%~f0"
        """;
}
