namespace Gaggle.Speech;

/// <summary>
/// Fetches a ggml Whisper model on first run. Models are far too large to commit,
/// so the app downloads the one named in config into %APPDATA%\Gaggle.
/// </summary>
public static class ModelInstaller
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

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
    /// </summary>
    public static async Task DownloadAsync(
        string modelFileName,
        string destinationPath,
        IProgress<double>? progress = null,
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

            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;

                if (total is > 0)
                {
                    progress?.Report((double)written / total.Value);
                }
            }
        }

        File.Move(tempPath, destinationPath, overwrite: true);
    }

    public sealed record ModelChoice(string FileName, string Name, string Notes);
}
