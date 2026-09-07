using System.Media;

namespace Gaggle.Audio;

/// <summary>
/// Three short tones that say what just happened without anything to look at.
///
/// This exists for hands-free flying in VR. Every other signal the app has - the review
/// overlay, the tray icon colour, balloon tips - is a desktop visual, and a pilot in a
/// headset sees none of them. A tone is the only channel left.
///
/// The tones are synthesised into a WAV buffer rather than shipped as assets: two files
/// of a few hundred samples are not worth carrying in the repository, and generating
/// them makes the interesting half - the buffer - testable without an audio device.
/// </summary>
public static class CueTones
{
    /// <summary>Plenty for a pure tone, and keeps the buffers tiny.</summary>
    private const int SampleRate = 16000;

    /// <summary>
    /// Length of the fade at each end. Starting a sine at full amplitude produces an
    /// audible click, which on a short tone is most of what you hear.
    /// </summary>
    private const int FadeSamples = 64;

    /// <summary>A short, high tick: recording has begun.</summary>
    public static byte[] StartedWav { get; } = BuildTone(880, 60);

    /// <summary>Higher and longer than the tick: the message reached chat.</summary>
    public static byte[] SentWav { get; } = BuildTone(1320, 90);

    /// <summary>Low and long, so it cannot be confused with either of the others.</summary>
    public static byte[] DroppedWav { get; } = BuildTone(320, 160);

    private static readonly Lazy<SoundPlayer?> Started = Prepare(StartedWav);
    private static readonly Lazy<SoundPlayer?> Sent = Prepare(SentWav);
    private static readonly Lazy<SoundPlayer?> Dropped = Prepare(DroppedWav);

    /// <summary>Recording has begun. A short, high tick.</summary>
    public static void PlayStarted() => Play(Started);

    /// <summary>The message reached chat. Higher and slightly longer than the tick.</summary>
    public static void PlaySent() => Play(Sent);

    /// <summary>
    /// Nothing was sent, for any reason - too quiet, rate limited, Condor not focused.
    /// Low and long, so it cannot be mistaken for either of the others in a headset.
    /// </summary>
    public static void PlayDropped() => Play(Dropped);

    /// <summary>
    /// A complete 16-bit mono PCM WAV holding one fading sine tone.
    ///
    /// Public because it is the part worth testing: the playback below needs a sound
    /// card, and this does not.
    /// </summary>
    public static byte[] BuildTone(int frequencyHz, int milliseconds, double amplitude = 0.25)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequencyHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(milliseconds);

        int samples = SampleRate * milliseconds / 1000;
        int dataBytes = samples * 2;

        using var buffer = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(buffer);

        // Canonical 44-byte RIFF/WAVE header. SoundPlayer will not touch a stream it
        // cannot parse, and says nothing when it declines, so this has to be exact.
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);                          // PCM chunk size
        writer.Write((short)1);                    // format: PCM
        writer.Write((short)1);                    // channels: mono
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);              // byte rate
        writer.Write((short)2);                    // block align
        writer.Write((short)16);                   // bits per sample
        writer.Write("data"u8);
        writer.Write(dataBytes);

        for (int i = 0; i < samples; i++)
        {
            double envelope = Math.Min(1.0, Math.Min(i, samples - 1 - i) / (double)FadeSamples);
            double value = Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate);

            writer.Write((short)(value * envelope * amplitude * short.MaxValue));
        }

        writer.Flush();

        return buffer.ToArray();
    }

    /// <summary>
    /// Readies the players away from the UI thread, so the first cue of a flight costs
    /// nothing there. Optional - the tones load on demand regardless - but the UI thread
    /// is the thread the keyboard hook lives on, and work it does not have to do there
    /// is work that cannot overrun the hook timeout.
    /// </summary>
    public static void Warm() => Task.Run(() =>
    {
        _ = Started.Value;
        _ = Sent.Value;
        _ = Dropped.Value;
    });

    /// <summary>
    /// Builds a player once, or null if this machine will not have it. Deferred because
    /// a tray app that cannot make a sound must still start.
    /// </summary>
    private static Lazy<SoundPlayer?> Prepare(byte[] wav) =>
        new(() =>
        {
            try
            {
                var player = new SoundPlayer(new MemoryStream(wav));
                player.Load();

                return player;
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or IOException)
            {
                return null;
            }
        });

    /// <summary>
    /// Fires the tone and returns. <see cref="SoundPlayer.Play"/> hands off to its own
    /// thread, which matters: callers are on the UI thread, one hop from a keyboard hook
    /// that Windows uninstalls if it blocks.
    ///
    /// A cue is feedback about something that already happened, so a machine with no
    /// audio device should fall silent rather than raise an error about it.
    /// </summary>
    private static void Play(Lazy<SoundPlayer?> tone)
    {
        try
        {
            tone.Value?.Play();
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or IOException)
        {
        }
    }
}
