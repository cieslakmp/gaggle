using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Gaggle.Input;

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

    /// <summary>Process name without .exe. Condor 2 ships as Condor.exe.</summary>
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

    /// <summary>Language hint, or "auto" to let Whisper decide.</summary>
    public string Language { get; set; } = "en";

    // -------------------------------------------------------------------- Text

    /// <summary>Longest message typed into chat. Verify against Condor and adjust.</summary>
    public int MaxMessageLength { get; set; } = 120;

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
    public static AppConfig Load()
    {
        Directory.CreateDirectory(DataDirectory);

        if (!File.Exists(ConfigPath))
        {
            var fresh = new AppConfig { PushToTalk = PttBinding.FromKey(Keys.CapsLock) };
            fresh.Save();
            return fresh;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            AppConfig loaded = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();

            // Configs written before joystick support only have TalkKey.
            loaded.PushToTalk ??= PttBinding.FromKey(loaded.TalkKey);

            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt config should not stop the app starting; fall back to defaults
            // and leave the bad file in place so the user can see what happened.
            return new AppConfig { PushToTalk = PttBinding.FromKey(Keys.CapsLock) };
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, SerializerOptions));
    }
}
