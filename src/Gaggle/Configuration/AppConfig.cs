using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Gaggle.Input;
using Gaggle.Speech;

namespace Gaggle.Configuration;

/// <summary>
/// User settings, stored as JSON in %APPDATA%\Gaggle\config.json.
///
/// Defaults are deliberately conservative: review before sending, a generous key
/// delay, and a rate limit. Tighten them once the timings are proven against a
/// real Condor install.
/// </summary>
public sealed class AppConfig
{
    // ------------------------------------------------------------------ Condor

    /// <summary>Process name without .exe. Condor 3 runs as Condor.exe.</summary>
    public string ProcessName { get; set; } = "Condor";

    /// <summary>Key that opens the in-game chat prompt.</summary>
    public Keys OpenChatKey { get; set; } = Keys.Back;

    /// <summary>Key that sends the typed message.</summary>
    public Keys SendChatKey { get; set; } = Keys.Enter;

    // ------------------------------------------------------------ Push to talk

    /// <summary>
    /// What the user holds to talk — a key or a joystick button. Null in configs
    /// written before joystick support, in which case <see cref="TalkKey"/> is
    /// migrated into it on load.
    /// </summary>
    public PttBinding? PushToTalk { get; set; }

    /// <summary>Superseded by <see cref="PushToTalk"/>; kept so old configs migrate.</summary>
    public Keys TalkKey { get; set; } = Keys.CapsLock;

    public Keys ConfirmKey { get; set; } = Keys.Enter;

    public Keys CancelKey { get; set; } = Keys.Escape;

    /// <summary>When false, a finished transcript is typed straight into chat.</summary>
    public bool ReviewBeforeSending { get; set; } = true;

    // ------------------------------------------------------------------ Timing

    /// <summary>Delay between injected keystrokes. Games poll input per frame, so
    /// anything much below this drops characters.</summary>
    public int KeyDelayMs { get; set; } = 30;

    /// <summary>Wait after the chat key before typing, to let the prompt appear.</summary>
    public int ChatOpenDelayMs { get; set; } = 200;

    /// <summary>Settle time between the last character and Enter.</summary>
    public int BeforeSendDelayMs { get; set; } = 120;

    /// <summary>Rate limit. Automated chat spam in a live race is antisocial.</summary>
    public int MinSecondsBetweenMessages { get; set; } = 3;

    /// <summary>Recording is abandoned past this, to bound transcription time.</summary>
    public int MaxRecordingSeconds { get; set; } = 15;

    // ------------------------------------------------------------------- Audio

    /// <summary>NAudio device index, or -1 for the system default.</summary>
    public int MicrophoneDeviceIndex { get; set; } = -1;

    /// <summary>RMS below this counts as silence and is never transcribed.</summary>
    public double SilenceThresholdRms { get; set; } = 0.005;

    // ------------------------------------------------------------------ Whisper

    /// <summary>ggml model file name, resolved inside the Gaggle data folder.</summary>
    public string WhisperModelFile { get; set; } = "ggml-base.en.bin";

    /// <summary>
    /// The language the pilot speaks: a two-letter Whisper code, or "auto". Anything
    /// other than "en" is translated to English before it reaches chat, and requires
    /// a multilingual model — the ".en" builds contain English and nothing else.
    /// </summary>
    public string Language { get; set; } = SpokenLanguage.EnglishCode;

    /// <summary>
    /// Decoder threads, or 0 for Whisper.net's default of every hardware thread.
    ///
    /// Transcription runs while Condor is rendering, so the default can leave the sim
    /// fighting for cores during a long transcription. Lower this — physical core
    /// count, or half of the logical count — if a message causes a frame-rate hitch.
    /// </summary>
    public int TranscriptionThreads { get; set; }

    /// <summary>
    /// Trims Whisper's fixed 30-second analysis window to the length actually spoken.
    /// Much faster on push-to-talk-length clips, at some risk to the transcript, which
    /// is why it can be turned off. See <see cref="Speech.TranscriptionOptions"/>.
    /// </summary>
    public bool FastTranscription { get; set; } = true;

    // -------------------------------------------------------------------- Text

    /// <summary>Longest message typed into chat. Verify against Condor and adjust.</summary>
    public int MaxMessageLength { get; set; } = 120;

    // ----------------------------------------------------------------- Updates

    /// <summary>
    /// Whether Gaggle looks for a new release in the background. The check runs at most
    /// once a day and talks only to the GitHub releases API; turn it off and the tray
    /// menu item is the only thing that ever reaches the network.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>When the last background check ran, so restarts do not re-check.</summary>
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    /// <summary>
    /// A version the user chose to skip. Background checks stay quiet about it; an
    /// explicit "Check for updates" ignores it, so a mis-click is recoverable.
    /// </summary>
    public string? SkippedVersion { get; set; }

    // ------------------------------------------------------------- Persistence

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [JsonIgnore]
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Gaggle");

    [JsonIgnore]
    public static string ConfigPath { get; } = Path.Combine(DataDirectory, "config.json");

    [JsonIgnore]
    public string ModelPath => Path.Combine(DataDirectory, WhisperModelFile);

    /// <summary>Loads config, writing a default file on first run.</summary>
    public static AppConfig Load() => LoadFrom(ConfigPath);

    /// <summary>
    /// Loads from an explicit path. Separate from <see cref="Load"/> so the defaults
    /// and the legacy-key migration can be tested without touching the real config.
    /// </summary>
    public static AppConfig LoadFrom(string path)
    {
        EnsureDirectory(path);

        if (!File.Exists(path))
        {
            AppConfig fresh = CreateDefault();
            fresh.SaveTo(path);
            return fresh;
        }

        try
        {
            string json = File.ReadAllText(path);
            AppConfig loaded = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? CreateDefault();

            // Configs written before joystick support only have TalkKey.
            loaded.PushToTalk ??= PttBinding.FromKey(loaded.TalkKey);

            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt config should not stop the app starting; fall back to defaults
            // and leave the bad file in place so the user can see what happened.
            return CreateDefault();
        }
    }

    public void Save() => SaveTo(ConfigPath);

    public void SaveTo(string path)
    {
        EnsureDirectory(path);
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    private static AppConfig CreateDefault() => new() { PushToTalk = PttBinding.FromKey(Keys.CapsLock) };

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
