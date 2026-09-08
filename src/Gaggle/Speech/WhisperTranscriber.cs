using Gaggle.Localisation;
using Whisper.net;

namespace Gaggle.Speech;

/// <summary>
/// Local speech-to-text via whisper.cpp (Whisper.net). Entirely offline: no API key,
/// no per-message cost, and no network round trip in the middle of a race.
///
/// The factory holds the loaded model and is expensive to build, so it is created
/// once and reused; each utterance gets a cheap processor. Nothing about the language
/// is baked in here, so switching language costs nothing and needs no reload.
/// </summary>
public sealed class WhisperTranscriber : IDisposable
{
    private readonly WhisperFactory _factory;

    private WhisperTranscriber(WhisperFactory factory) => _factory = factory;

    /// <summary>Loads a ggml model from disk. Throws if the file is missing or invalid.</summary>
    public static WhisperTranscriber Load(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                Strings.Current.ModelFileNotFound(modelPath),
                modelPath);
        }

        return new WhisperTranscriber(WhisperFactory.FromPath(modelPath));
    }

    /// <summary>
    /// Transcribes a 16 kHz mono WAV stream, translating to English unless the spoken
    /// language already is English. Returns the joined segment text, which still needs
    /// sanitising before it goes anywhere near the chat prompt.
    /// </summary>
    public async Task<string> TranscribeAsync(
        Stream wav,
        TranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        wav.Position = 0;

        WhisperProcessorBuilder builder = _factory.CreateBuilder();

        builder = SpokenLanguage.IsAuto(options.Language)
            ? builder.WithLanguageDetection()
            : builder.WithLanguage(options.Language);

        if (options.TranslateToEnglish)
        {
            // Whisper's second task. It decodes straight to English rather than
            // transcribing and then translating, so there is no second model and no
            // second pass — this is the whole feature, in one call.
            builder = builder.WithTranslate();
        }

        // A push-to-talk burst is one short sentence, so none of the machinery for
        // long-form audio earns its cost here: no beam search, no carrying the last
        // message in as a prompt (which also stops one chat line bleeding into the
        // next), and no hunting for segment boundaries.
        builder = builder
            .WithGreedySamplingStrategy(greedy => greedy.WithBestOf(1))
            .WithNoContext()
            .WithSingleSegment();

        if (options.Threads > 0)
        {
            builder = builder.WithThreads(options.Threads);
        }

        if (options.AudioContextSize > 0)
        {
            builder = builder.WithAudioContextSize(options.AudioContextSize);
        }

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
