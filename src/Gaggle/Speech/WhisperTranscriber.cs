using Whisper.net;

namespace Gaggle.Speech;

/// <summary>
/// Local speech-to-text via whisper.cpp (Whisper.net). Entirely offline: no API key,
/// no per-message cost, and no network round trip in the middle of a race.
///
/// The factory holds the loaded model and is expensive to build, so it is created
/// once and reused; each utterance gets a cheap processor.
/// </summary>
public sealed class WhisperTranscriber : IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly string _language;

    private WhisperTranscriber(WhisperFactory factory, string language)
    {
        _factory = factory;
        _language = language;
    }

    /// <summary>Loads a ggml model from disk. Throws if the file is missing or invalid.</summary>
    public static WhisperTranscriber Load(string modelPath, string language)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Whisper model not found at {modelPath}. Download one from the tray menu.",
                modelPath);
        }

        return new WhisperTranscriber(WhisperFactory.FromPath(modelPath), language);
    }

    /// <summary>
    /// Transcribes a 16 kHz mono WAV stream. Returns the joined segment text, which
    /// still needs sanitising before it goes anywhere near the chat prompt.
    /// </summary>
    public async Task<string> TranscribeAsync(Stream wav, CancellationToken cancellationToken = default)
    {
        wav.Position = 0;

        WhisperProcessorBuilder builder = _factory.CreateBuilder();

        builder = string.Equals(_language, "auto", StringComparison.OrdinalIgnoreCase)
            ? builder.WithLanguageDetection()
            : builder.WithLanguage(_language);

        await using WhisperProcessor processor = builder.Build();

        var text = new System.Text.StringBuilder();

        await foreach (SegmentData segment in processor.ProcessAsync(wav, cancellationToken))
        {
            text.Append(segment.Text);
        }

        return text.ToString();
    }

    public void Dispose() => _factory.Dispose();
}
