using Gaggle.Localisation;
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
        new("ggml-tiny.en.bin", "Tiny (English)", "~75 MB, fastest, noticeably weaker on jargon",
            "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f", 77_704_715),
        new("ggml-base.en.bin", "Base (English)", "~148 MB, good default for short radio calls",
            "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002", 147_964_211),
        new("ggml-small.en.bin", "Small (English)", "~488 MB, best accuracy, ~2x slower",
            "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d", 487_614_201),
        new("ggml-small.bin", "Small (multilingual)", "~488 MB, needed for Polish/German/Spanish",
            "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b", 487_601_967),
        new("ggml-medium.bin", "Medium (multilingual)", "~1.5 GB, best non-English accuracy, ~3x slower",
            "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208", 1_533_763_059),
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
    public static async Task DownloadAsync(
        ModelChoice choice,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(choice);

        await FileDownloader.DownloadAsync(
            BaseUrl + choice.FileName,
            destinationPath,
            choice.Bytes,
            progress,
            cancellationToken);

        string actual = await FileDownloader.ComputeSha256Async(destinationPath, cancellationToken);

        if (!string.Equals(actual, choice.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            // Deleted rather than left on disk: a file this size looks installed, and the
            // menu would tick it and never offer to fetch it again.
            FileDownloader.TryDelete(destinationPath);

            throw new InvalidOperationException(Strings.Current.ModelChecksumMismatch(choice.Name));
        }
    }

    /// <param name="Sha256">
    /// The digest Hugging Face publishes for this file, as the lowercase hex
    /// <c>ComputeSha256Async</c> returns.
    /// </param>
    /// <param name="Bytes">The exact published size, used to bound the download.</param>
    public sealed record ModelChoice(string FileName, string Name, string Notes, string Sha256, long Bytes)
    {
        /// <summary>Whether this build can handle anything other than English.</summary>
        public bool IsMultilingual => IsMultilingualFile(FileName);
    }
}
