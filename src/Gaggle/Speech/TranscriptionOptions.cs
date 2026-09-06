namespace Gaggle.Speech;

/// <summary>
/// Per-utterance decoding settings. These live here rather than on
/// <see cref="WhisperTranscriber"/> because the expensive part is the loaded model,
/// which is reused; everything below is chosen fresh for each push-to-talk burst and
/// so can change without reloading anything.
/// </summary>
public readonly record struct TranscriptionOptions
{
    /// <summary>Whisper's fixed window is 30 seconds wide and 1500 encoder positions long.</summary>
    private const int FullAudioContext = 1500;

    private const double WindowSeconds = 30.0;

    /// <summary>
    /// Below this the encoder loses too much to recover, so short clips stop shrinking
    /// here rather than being trimmed to nothing.
    /// </summary>
    private const int MinimumAudioContext = 512;

    /// <summary>Spoken language: a two-letter Whisper code, or "auto".</summary>
    public required string Language { get; init; }

    /// <summary>Decoder threads, or 0 to leave Whisper.net's default alone.</summary>
    public int Threads { get; init; }

    /// <summary>
    /// Encoder positions to process, or 0 for Whisper's full window. See
    /// <see cref="AudioContextFor"/> for why shrinking this is worth the risk.
    /// </summary>
    public int AudioContextSize { get; init; }

    /// <summary>
    /// True when the transcript needs Whisper's translate task. Everything that is not
    /// explicitly English gets it, "auto" included: under auto-detection the spoken
    /// language is unknown until decoding has already started.
    /// </summary>
    public bool TranslateToEnglish => !SpokenLanguage.IsEnglish(Language);

    /// <summary>
    /// Encoder positions worth spending on a clip of the given length.
    ///
    /// Whisper pads every clip out to its full 30 second window and runs the encoder
    /// over all of it, so a two second radio call costs the same as a half-minute
    /// monologue. Overriding the audio context skips the padding. It is the single
    /// biggest saving available for push-to-talk-length audio, and it is also the one
    /// setting here that can degrade the transcript, which is why it is gated behind
    /// <c>FastTranscription</c> in config.
    ///
    /// The headroom matters: sized to exactly the clip, the encoder tends to clip the
    /// tail of the last word.
    /// </summary>
    public static int AudioContextFor(TimeSpan clip)
    {
        if (clip <= TimeSpan.Zero)
        {
            return FullAudioContext;
        }

        double needed = clip.TotalSeconds * (FullAudioContext / WindowSeconds) * 1.25;

        // Rounded up to a multiple of 64 so the value lands on a sensible boundary
        // rather than wherever the clip length happened to fall.
        int rounded = (int)(Math.Ceiling(needed / 64) * 64);

        return Math.Clamp(rounded, MinimumAudioContext, FullAudioContext);
    }
}
