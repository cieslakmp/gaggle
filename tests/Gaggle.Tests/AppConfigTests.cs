using System.Windows.Forms;
using Gaggle.Configuration;
using Gaggle.Input;
using Gaggle.Localisation;
using Gaggle.Speech;

namespace Gaggle.Tests;

/// <summary>
/// Uses a throwaway directory per test, so nothing here can touch the real
/// %APPDATA%\Gaggle\config.json.
/// </summary>
public class AppConfigTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public AppConfigTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "gaggle-tests-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_directory, "config.json");
    }

    [Fact]
    public void WritesADefaultFileOnFirstRun()
    {
        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.True(File.Exists(_path));
        Assert.Equal("Condor", config.ProcessName);

        // The three defaults that decide what reaches a live race chat without anyone
        // having agreed to it. Changing any of them is a product decision, not a tidy-up.
        Assert.True(config.ReviewBeforeSending);
        Assert.False(config.AudibleFeedback);
        Assert.Equal(5, config.HandsFreeMaxRecordingSeconds);
    }

    [Fact]
    public void AConfigWrittenBeforeHandsFreeKeepsTheSafeDefaults()
    {
        // Everything an existing install has on disk, and none of the new keys. The
        // upgrade must not quietly turn review off or start making noise.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "ProcessName": "Condor", "MaxRecordingSeconds": 20 }""");

        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.True(config.ReviewBeforeSending);
        Assert.False(config.AudibleFeedback);
        Assert.Equal(5, config.HandsFreeMaxRecordingSeconds);
        Assert.Equal(20, config.MaxRecordingSeconds);
    }

    [Fact]
    public void DefaultBindingIsCapsLock()
    {
        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.NotNull(config.PushToTalk);
        Assert.Equal(PttSource.Keyboard, config.PushToTalk.Source);
        Assert.Equal(Keys.CapsLock, config.PushToTalk.Key);
    }

    [Fact]
    public void MigratesLegacyTalkKeyIntoABinding()
    {
        // A config written before joystick support: TalkKey, no PushToTalk.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "ProcessName": "Condor", "TalkKey": "F13" }""");

        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.NotNull(config.PushToTalk);
        Assert.Equal(PttSource.Keyboard, config.PushToTalk.Source);
        Assert.Equal(Keys.F13, config.PushToTalk.Key);
    }

    [Fact]
    public void DoesNotOverwriteAnExistingBindingWhenMigrating()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _path,
            """{ "TalkKey": "F13", "PushToTalk": { "Source": "Joystick", "JoystickId": 1, "Button": 5 } }""");

        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.Equal(PttSource.Joystick, config.PushToTalk!.Source);
        Assert.Equal(1, config.PushToTalk.JoystickId);
        Assert.Equal(5, config.PushToTalk.Button);
    }

    [Fact]
    public void FallsBackToDefaultsOnCorruptJsonAndLeavesTheFileAlone()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, "{ this is not json");

        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.Equal("Condor", config.ProcessName);
        Assert.NotNull(config.PushToTalk);

        // The bad file stays put so the user can see what happened.
        Assert.Equal("{ this is not json", File.ReadAllText(_path));
    }

    [Fact]
    public void SettingsSurviveASaveAndReload()
    {
        AppConfig original = AppConfig.LoadFrom(_path);
        original.ProcessName = "Condor3";
        original.ChatOpenDelayMs = 450;
        original.MaxMessageLength = 90;
        original.ReviewBeforeSending = false;
        original.AudibleFeedback = true;
        original.HandsFreeMaxRecordingSeconds = 7;
        original.PushToTalk = PttBinding.FromButton(2, 9);
        original.SaveTo(_path);

        AppConfig reloaded = AppConfig.LoadFrom(_path);

        Assert.Equal("Condor3", reloaded.ProcessName);
        Assert.Equal(450, reloaded.ChatOpenDelayMs);
        Assert.Equal(90, reloaded.MaxMessageLength);
        Assert.False(reloaded.ReviewBeforeSending);
        Assert.True(reloaded.AudibleFeedback);
        Assert.Equal(7, reloaded.HandsFreeMaxRecordingSeconds);
        Assert.Equal(PttSource.Joystick, reloaded.PushToTalk!.Source);
        Assert.Equal(2, reloaded.PushToTalk.JoystickId);
        Assert.Equal(9, reloaded.PushToTalk.Button);
    }

    /// <summary>
    /// The recording limits are the cancel gesture: hold past one and the recording is
    /// thrown away. They match today, and hands-free must never become the longer of the
    /// two — it is the mode with no overlay to watch and no Escape to press, so it cannot
    /// be the one that holds on longer before giving the pilot their message back.
    /// </summary>
    [Fact]
    public void HandsFreeGivesUpNoLaterThanReviewDoes()
    {
        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.Equal(5, config.MaxRecordingSeconds);
        Assert.True(
            config.HandsFreeMaxRecordingSeconds <= config.MaxRecordingSeconds,
            "Hands-free would hold a recording longer than review mode does.");
    }

    /// <summary>
    /// UiLanguage is a new key, and the way a new key goes wrong is by being dropped:
    /// it has to serialise by name like the rest and come back as what was written.
    /// </summary>
    [Fact]
    public void TheInterfaceLanguageSurvivesJsonRoundTrip()
    {
        AppConfig original = AppConfig.LoadFrom(_path);

        original.UiLanguage = UiLanguage.Polish;
        original.SaveTo(_path);

        Assert.Contains("\"UiLanguage\": \"Polish\"", File.ReadAllText(_path), StringComparison.Ordinal);
        Assert.Equal(UiLanguage.Polish, AppConfig.LoadFrom(_path).UiLanguage);
    }

    /// <summary>
    /// A config written before this setting existed has no key for it, so the property
    /// keeps its initialiser — the Windows display language. That is the intended
    /// behaviour on an upgrade and not a value read from the file.
    /// </summary>
    [Fact]
    public void AConfigWrittenBeforeTheInterfaceLanguageFallsBackToWindows()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_path, """{ "ProcessName": "Condor" }""");

        Assert.Equal(Strings.FromSystem(), AppConfig.LoadFrom(_path).UiLanguage);
    }

    /// <summary>
    /// The two languages are unrelated settings that both read as "language", so this
    /// pins the one thing that must never quietly become true: that changing what the
    /// pilot speaks changed what the menus say, or the other way round.
    /// </summary>
    [Fact]
    public void TheSpokenLanguageAndTheInterfaceLanguageAreIndependent()
    {
        AppConfig config = AppConfig.LoadFrom(_path);

        config.UiLanguage = UiLanguage.Polish;
        config.Language = SpokenLanguage.EnglishCode;
        config.SaveTo(_path);

        AppConfig reloaded = AppConfig.LoadFrom(_path);

        Assert.Equal(UiLanguage.Polish, reloaded.UiLanguage);
        Assert.Equal(SpokenLanguage.EnglishCode, reloaded.Language);
    }

    [Fact]
    public void ModelPathSitsInTheDataDirectory()
    {
        AppConfig config = AppConfig.LoadFrom(_path);

        Assert.Equal(Path.Combine(AppConfig.DataDirectory, config.WhisperModelFile), config.ModelPath);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
