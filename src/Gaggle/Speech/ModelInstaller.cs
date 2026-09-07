using Gaggle.Net;

namespace Gaggle.Speech;

/// <summary>
/// Fetches a ggml Whisper model on first run. Models are far too large to commit,
/// so the app downloads the one named in config into %APPDATA%\Gaggle.
/// </summary>
public static class ModelInstaller
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    /// <summary>
    /// Models worth offering, English-only first and then multilingual, each group
    /// smallest first. Sizes are approximate.
    ///
    /// The ".en" builds are faster and marginally better at English, but they contain
    /// only English and have no translate task at all, so they cannot serve anything
    /// but <see cref="SpokenLanguage.EnglishCode"/>. The multilingual builds are the
    /// same models without that restriction, and they are the ones that can turn
    /// Polish, German or Spanish speech into English chat.
    /// </summary>
    public static readonly IReadOnlyList<ModelChoice> Available =
    [
        new("ggml-tiny.en.bin", "Tiny (English)", "~75 MB, fastest, noticeably weaker on jargon"),
        new("ggml-base.en.bin", "Base (English)", "~148 MB, good default for short radio calls"),
        new("ggml-small.en.bin", "Small (English)", "~488 MB, best accuracy, ~2x slower"),
        new("ggml-small.bin", "Small (multilingual)", "~488 MB, needed for Polish/German/Spanish"),
        new("ggml-medium.bin", "Medium (multilingual)", "~1.5 GB, best non-English accuracy, ~3x slower"),
    ];

    public static bool IsInstalled(string modelPath) => File.Exists(modelPath);

    /// <summary>
    /// True when a ggml file name is a multilingual build. Takes the file name rather
    /// than a <see cref="ModelChoice"/> so a hand-edited <c>WhisperModelFile</c> that
    /// names no listed model is still classified correctly.
    /// </summary>
    public static bool IsMultilingualFile(string fileName) =>
        !fileName.EndsWith(".en.bin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The English-only build to offer in place of <paramref name="multilingualFile"/>:
    /// the same tier where one is listed (small -> small.en), otherwise the most
    /// capable English build there is, so switching to English never quietly drops
    /// someone from Medium to Tiny.
    /// </summary>
    public static ModelChoice CounterpartEnglish(string multilingualFile)
    {
        string sameTier = multilingualFile.Replace(".bin", ".en.bin", StringComparison.OrdinalIgnoreCase);

        foreach (ModelChoice choice in Available)
        {
            if (!choice.IsMultilingual
                && string.Equals(choice.FileName, sameTier, StringComparison.OrdinalIgnoreCase))
            {
                return choice;
            }
        }

        // Available is ordered smallest first, so the last English build is the best.
        return Available.Last(choice => !choice.IsMultilingual);
    }

    /// <summary>
    /// Downloads a model into the Gaggle data folder. The streaming, progress reporting
    /// and partial-file handling live in <see cref="FileDownloader"/>, which the updater
    /// shares.
    /// </summary>
    public static Task DownloadAsync(
        string modelFileName,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        FileDownloader.DownloadAsync(BaseUrl + modelFileName, destinationPath, progress, cancellationToken);

    public sealed record ModelChoice(string FileName, string Name, string Notes)
    {
        /// <summary>Whether this build can handle anything other than English.</summary>
        public bool IsMultilingual => IsMultilingualFile(FileName);
    }
}
