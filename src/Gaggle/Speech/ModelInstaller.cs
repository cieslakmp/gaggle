namespace Gaggle.Speech;

/// <summary>
/// Fetches a ggml Whisper model on first run. Models are far too large to commit,
/// so the app downloads the one named in config into %APPDATA%\Gaggle.
/// </summary>
public static class ModelInstaller
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    /// <summary>Minimum gap between progress reports.</summary>
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Models worth offering, smallest first. Sizes are approximate.</summary>
    public static readonly IReadOnlyList<ModelChoice> Available =
    [
        new("ggml-tiny.en.bin", "Tiny (English)", "~75 MB, fastest, noticeably weaker on jargon"),
        new("ggml-base.en.bin", "Base (English)", "~148 MB, good default for short radio calls"),
        new("ggml-small.en.bin", "Small (English)", "~488 MB, best accuracy, ~2x slower"),
    ];

    public static bool IsInstalled(string modelPath) => File.Exists(modelPath);

    /// <summary>
    /// Downloads the model to a temporary file and moves it into place once complete,
    /// so an interrupted download never leaves a half-written model that fails to load.
    ///
    /// Progress is reported at most every <see cref="ReportInterval"/>, because these
    /// files are large enough that reporting every buffer would flood the UI thread.
    /// </summary>
    public static async Task DownloadAsync(
        string modelFileName,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        string tempPath = destinationPath + ".partial";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        using HttpResponseMessage response = await http.GetAsync(
            BaseUrl + modelFileName,
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

    public sealed record ModelChoice(string FileName, string Name, string Notes);

    /// <summary>
    /// Bytes received so far, and the total when the server declares one. A null
    /// total means the response had no Content-Length, so only bytes can be shown.
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
}
