using System.Security.Cryptography;

namespace Gaggle.Net;

/// <summary>
/// One streaming HTTP download, shared by the ggml model installer and the updater.
///
/// The download goes to a ".partial" file and is moved into place only once it has
/// finished, so an interrupted transfer never leaves a truncated file behind — which for
/// a model means a load failure, and for an update package would mean extracting rubbish
/// over a working install.
/// </summary>
public static class FileDownloader
{
    /// <summary>Minimum gap between progress reports.</summary>
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How we identify ourselves. The GitHub API rejects requests that send no
    /// User-Agent at all, so this is required rather than decorative.
    /// </summary>
    public static string UserAgent { get; } = $"{AppInfo.Name}/{AppInfo.Version}";

    /// <summary>An HttpClient carrying the User-Agent every caller here needs.</summary>
    public static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        return client;
    }

    /// <summary>
    /// Downloads a URL to a file, reporting progress at most every
    /// <see cref="ReportInterval"/>. These files run to hundreds of megabytes, so
    /// reporting every buffer would flood the UI thread.
    /// </summary>
    public static async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        string tempPath = destinationPath + ".partial";

        using HttpClient http = CreateClient(TimeSpan.FromMinutes(30));

        using HttpResponseMessage response = await http.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;

        await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[81_920];
            long written = 0;
            int read;
            // Seeded to now, so the opening report below is not immediately repeated
            // by the first loop iteration.
            long lastReportTicks = Environment.TickCount64;

            progress?.Report(new DownloadProgress(0, total));

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;

                long now = Environment.TickCount64;
                if (now - lastReportTicks >= ReportInterval.TotalMilliseconds)
                {
                    lastReportTicks = now;
                    progress?.Report(new DownloadProgress(written, total));
                }
            }

            progress?.Report(new DownloadProgress(written, total ?? written));
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    /// <summary>Fetches a small text resource, for checksums and API responses.</summary>
    public static async Task<string> GetStringAsync(
        string url,
        TimeSpan timeout,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        using HttpClient http = CreateClient(timeout);

        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                http.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        return await http.GetStringAsync(url, cancellationToken);
    }

    /// <summary>The SHA256 of a file as lowercase hex, to match what sha256sum prints.</summary>
    public static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);

        return Convert.ToHexStringLower(hash);
    }
}

/// <summary>
/// Bytes received so far, and the total when the server declares one. A null total means
/// the response had no Content-Length, so only bytes can be shown.
/// </summary>
public readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;

    /// <summary>Human-readable form, e.g. "42% — 62.1 of 141.1 MB".</summary>
    public string Describe()
    {
        const double Megabyte = 1024 * 1024;

        if (TotalBytes is > 0)
        {
            return $"{Fraction!.Value:P0} — {BytesReceived / Megabyte:F1} of {TotalBytes.Value / Megabyte:F1} MB";
        }

        return $"{BytesReceived / Megabyte:F1} MB";
    }
}
