using System.Windows.Forms;
using Gaggle.Configuration;
using Gaggle.Input;

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
        Assert.Equal(10, config.HandsFreeMaxRecordingSeconds);

        // The guide is written in English until someone chooses otherwise, and it has
        // not been shown yet.
        Assert.Equal("en", config.UiLanguage);
        Assert.False(config.OnboardingSeen);
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
        Assert.Equal(10, config.HandsFreeMaxRecordingSeconds);
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
        original.UiLanguage = "pl";
        original.OnboardingSeen = true;
        original.PushToTalk = PttBinding.FromButton(2, 9);
        original.SaveTo(_path);

        AppConfig reloaded = AppConfig.LoadFrom(_path);

        Assert.Equal("Condor3", reloaded.ProcessName);
        Assert.Equal(450, reloaded.ChatOpenDelayMs);
        Assert.Equal(90, reloaded.MaxMessageLength);
        Assert.False(reloaded.ReviewBeforeSending);
        Assert.True(reloaded.AudibleFeedback);
        Assert.Equal(7, reloaded.HandsFreeMaxRecordingSeconds);
        Assert.Equal("pl", reloaded.UiLanguage);
        Assert.True(reloaded.OnboardingSeen);
        Assert.Equal(PttSource.Joystick, reloaded.PushToTalk!.Source);
        Assert.Equal(2, reloaded.PushToTalk.JoystickId);
        Assert.Equal(9, reloaded.PushToTalk.Button);
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
