using System.Diagnostics;
using System.Windows.Forms;
using Gaggle.Audio;
using Gaggle.Condor;
using Gaggle.Configuration;
using Gaggle.Input;
using Gaggle.Interop;
using Gaggle.Speech;
using Gaggle.Text;

namespace Gaggle.Ui;

/// <summary>
/// Wires the pieces together and owns the push-to-talk state machine:
///
///   Idle -> Recording (key down) -> Transcribing (key up) -> Review -> Idle
///
/// Everything raised by the keyboard hook is deferred with BeginInvoke. The hook
/// callback has a 300 ms budget before Windows uninstalls it, so no handler may do
/// real work inline.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly CondorWatcher _watcher;
    private readonly ChatSender _sender;
    private readonly MicrophoneRecorder _recorder = new();
    private readonly PushToTalkHook _hook = new();
    private readonly JoystickWatcher _joystick = new();
    private readonly PttController _controller;
    private readonly ReviewOverlay _overlay = new();
    private readonly System.Windows.Forms.Timer _recordingLimit;
    private readonly System.Windows.Forms.Timer _statusTimeout;

    /// <summary>NotifyIcon.Text throws above this length.</summary>
    private const int TrayTextLimit = 63;

    private WhisperTranscriber? _transcriber;
    private string? _pendingMessage;
    private bool _busy;
    private bool _downloading;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();

        _watcher = new CondorWatcher(_config.ProcessName);
        _sender = new ChatSender(_watcher);

        _statusItem = new ToolStripMenuItem("Starting…") { Enabled = false };
        _menu = BuildMenu();

        _tray = new NotifyIcon
        {
            Icon = TrayIcons.Create(TrayIcons.Idle),
            Text = "Gaggle",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        _recordingLimit = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(1, _config.MaxRecordingSeconds) * 1000,
        };
        _recordingLimit.Tick += (_, _) => StopRecordingAndTranscribe();

        _statusTimeout = new System.Windows.Forms.Timer { Interval = 2500 };
        _statusTimeout.Tick += (_, _) =>
        {
            _statusTimeout.Stop();

            if (_pendingMessage is null)
            {
                _overlay.HideOverlay();
            }
        };

        _watcher.StateChanged += (_, _) => RefreshStatus();
        _watcher.Start();

        _recorder.Failed += ex => BeginInvokeOnUi(() => ShowStatus($"Microphone error: {ex.Message}", isError: true));

        _controller = new PttController(_hook, _joystick);
        ConfigureInput();

        try
        {
            _hook.Install();
            _controller.Start();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(
                ex.Message + "\n\nPush-to-talk will not work. Gaggle will keep running so you can check settings.",
                "Gaggle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        RefreshStatus();
        _ = LoadModelAsync();
    }

    // ------------------------------------------------------------------ Wiring

    private void ConfigureInput()
    {
        _hook.ConfirmKey = _config.ConfirmKey;
        _hook.CancelKey = _config.CancelKey;

        _hook.Confirmed += () => BeginInvokeOnUi(SendPending);
        _hook.Cancelled += () => BeginInvokeOnUi(DiscardPending);

        _controller.Pressed += () => BeginInvokeOnUi(StartRecording);
        _controller.Released += () => BeginInvokeOnUi(StopRecordingAndTranscribe);
        _controller.Binding = _config.PushToTalk ?? PttBinding.FromKey(_config.TalkKey);
    }

    /// <summary>
    /// Marshals to the UI thread via the overlay handle. The overlay is created in
    /// the constructor on the UI thread, so its handle is the reliable one to use.
    /// </summary>
    private void BeginInvokeOnUi(Action action)
    {
        if (_overlay.IsDisposed)
        {
            return;
        }

        if (!_overlay.IsHandleCreated)
        {
            _ = _overlay.Handle; // Force creation.
        }

        _overlay.BeginInvoke(action);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        var microphones = new ToolStripMenuItem("Microphone");
        microphones.DropDownOpening += (_, _) => PopulateMicrophones(microphones);
        menu.Items.Add(microphones);

        var models = new ToolStripMenuItem("Speech model");
        models.DropDownOpening += (_, _) => PopulateModels(models);
        menu.Items.Add(models);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Push-to-talk…", null, (_, _) => ShowSettings());
        menu.Items.Add("Open config file", null, (_, _) => OpenConfig());
        menu.Items.Add("Reload config", null, (_, _) => ReloadConfig());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        return menu;
    }

    // -------------------------------------------------------------- Model load

    private async Task LoadModelAsync()
    {
        if (!ModelInstaller.IsInstalled(_config.ModelPath))
        {
            RefreshStatus();
            _tray.ShowBalloonTip(
                8000,
                "Gaggle",
                "No speech model installed yet. Pick one from the tray menu under Speech model.",
                ToolTipIcon.Info);
            return;
        }

        try
        {
            string modelPath = _config.ModelPath;
            string language = _config.Language;

            WhisperTranscriber loaded = await Task.Run(() => WhisperTranscriber.Load(modelPath, language));

            _transcriber?.Dispose();
            _transcriber = loaded;
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(8000, "Gaggle", $"Could not load the speech model: {ex.Message}", ToolTipIcon.Error);
        }

        RefreshStatus();
    }

    // --------------------------------------------------------- Push to talk

    private void StartRecording()
    {
        if (_busy || _recorder.IsRecording)
        {
            return;
        }

        if (_transcriber is null)
        {
            ShowStatus("No speech model loaded — see the tray menu.", isError: true);
            return;
        }

        if (!_watcher.IsRunning)
        {
            ShowStatus($"{_config.ProcessName} is not running.", isError: true);
            return;
        }

        DiscardPending();

        try
        {
            _recorder.Start(_config.MicrophoneDeviceIndex);
        }
        catch (Exception ex)
        {
            ShowStatus($"Microphone unavailable: {ex.Message}", isError: true);
            return;
        }

        _recordingLimit.Start();
        _tray.Icon = TrayIcons.Create(TrayIcons.Recording);
        _overlay.ShowStatus("Listening…");
        _statusTimeout.Stop();
    }

    private void StopRecordingAndTranscribe()
    {
        _recordingLimit.Stop();

        if (!_recorder.IsRecording)
        {
            return;
        }

        MemoryStream? audio = _recorder.Stop();
        _tray.Icon = TrayIcons.Create(TrayIcons.Working);

        if (audio is null)
        {
            ShowStatus("Nothing recorded.", isError: true);
            RefreshStatus();
            return;
        }

        _overlay.ShowStatus("Transcribing…");
        _ = TranscribeAsync(audio);
    }

    private async Task TranscribeAsync(MemoryStream audio)
    {
        _busy = true;

        try
        {
            double level = MicrophoneRecorder.CalculateRms(audio);

            if (level < _config.SilenceThresholdRms)
            {
                // Fed silence, Whisper invents plausible sentences. Never transcribe it.
                ShowStatus("Too quiet — nothing sent.", isError: true);
                return;
            }

            WhisperTranscriber? transcriber = _transcriber;
            if (transcriber is null)
            {
                ShowStatus("No speech model loaded.", isError: true);
                return;
            }

            string raw = await transcriber.TranscribeAsync(audio);
            string? message = MessageSanitiser.Clean(raw, _config.MaxMessageLength);

            if (message is null)
            {
                ShowStatus("Did not catch that.", isError: true);
                return;
            }

            if (_config.ReviewBeforeSending)
            {
                _pendingMessage = message;
                _hook.ReviewPending = true;
                _overlay.ShowTranscript(message, _config.ConfirmKey, _config.CancelKey);
            }
            else
            {
                _pendingMessage = message;
                SendPending();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Transcription failed: {ex.Message}", isError: true);
        }
        finally
        {
            _busy = false;
            await audio.DisposeAsync();
            RefreshStatus();
        }
    }

    private void SendPending()
    {
        string? message = _pendingMessage;

        _pendingMessage = null;
        _hook.ReviewPending = false;

        if (message is null)
        {
            return;
        }

        _overlay.ShowStatus("Sending…");
        _ = SendAsync(message);
    }

    private async Task SendAsync(string message)
    {
        SendResult result = await _sender.SendAsync(message, _config);

        if (result.Success)
        {
            _overlay.HideOverlay();
        }
        else
        {
            ShowStatus(result.Describe(), isError: true);
        }

        RefreshStatus();
    }

    private void DiscardPending()
    {
        if (_pendingMessage is null && !_hook.ReviewPending)
        {
            return;
        }

        _pendingMessage = null;
        _hook.ReviewPending = false;
        _overlay.HideOverlay();
    }

    // -------------------------------------------------------------- Menu items

    private void PopulateMicrophones(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        IReadOnlyList<string> devices = MicrophoneRecorder.ListDevices();

        if (devices.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem("No microphones found") { Enabled = false });
            return;
        }

        for (int i = 0; i < devices.Count; i++)
        {
            int index = i;

            var item = new ToolStripMenuItem(devices[i])
            {
                Checked = _config.MicrophoneDeviceIndex == index
                    || (_config.MicrophoneDeviceIndex < 0 && index == 0),
            };

            item.Click += (_, _) =>
            {
                _config.MicrophoneDeviceIndex = index;
                _config.Save();
            };

            parent.DropDownItems.Add(item);
        }
    }

    private void PopulateModels(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            bool installed = File.Exists(Path.Combine(AppConfig.DataDirectory, choice.FileName));

            var item = new ToolStripMenuItem($"{choice.Name} — {choice.Notes}")
            {
                Checked = _config.WhisperModelFile == choice.FileName,
            };

            item.Click += async (_, _) => await SelectModelAsync(choice, installed);
            parent.DropDownItems.Add(item);
        }
    }

    private async Task SelectModelAsync(ModelInstaller.ModelChoice choice, bool installed)
    {
        if (_downloading)
        {
            ShowStatus("A model download is already running.", isError: true);
            return;
        }

        string destination = Path.Combine(AppConfig.DataDirectory, choice.FileName);

        if (!installed && !await DownloadModelAsync(choice, destination))
        {
            return;
        }

        _config.WhisperModelFile = choice.FileName;
        _config.Save();

        await LoadModelAsync();
    }

    /// <summary>
    /// Downloads a model, reporting progress in the overlay and the tray tooltip.
    /// These files run to hundreds of megabytes, so silence here reads as a hang.
    /// </summary>
    private async Task<bool> DownloadModelAsync(ModelInstaller.ModelChoice choice, string destination)
    {
        DialogResult answer = MessageBox.Show(
            $"Download {choice.Name} ({choice.Notes}) from Hugging Face?",
            "Gaggle",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (answer != DialogResult.OK)
        {
            return false;
        }

        // Constructed on the UI thread, so its callbacks arrive there too.
        var progress = new Progress<ModelInstaller.DownloadProgress>(report =>
        {
            string text = $"Downloading {choice.Name} — {report.Describe()}";
            _overlay.ShowStatus(text);
            _tray.Text = Truncate($"Gaggle — {text}", TrayTextLimit);
        });

        _downloading = true;
        _busy = true; // Push-to-talk would have no model to use anyway.
        _statusTimeout.Stop();
        _tray.Icon = TrayIcons.Create(TrayIcons.Working);

        try
        {
            await ModelInstaller.DownloadAsync(choice.FileName, destination, progress);
            ShowStatus($"{choice.Name} installed.");
            return true;
        }
        catch (Exception ex)
        {
            _overlay.HideOverlay();
            MessageBox.Show($"Download failed: {ex.Message}", "Gaggle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            _downloading = false;
            _busy = false;
            RefreshStatus();
        }
    }

    /// <summary>NotifyIcon.Text throws above 63 characters.</summary>
    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    private void ShowSettings()
    {
        // ShowDialog keeps pumping messages, so both the keyboard hook and the
        // joystick poll timer stay live and can capture a new binding.
        using var form = new SettingsForm(_controller, _controller.Binding);

        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        _controller.Binding = form.Binding;
        _config.PushToTalk = form.Binding;

        if (form.Binding.IsKeyboard)
        {
            _config.TalkKey = form.Binding.Key;
        }

        _config.Save();
        RefreshStatus();
    }

    private void OpenConfig()
    {
        _config.Save(); // Make sure the file exists before opening it.
        Process.Start(new ProcessStartInfo(AppConfig.ConfigPath) { UseShellExecute = true });
    }

    private void ReloadConfig()
    {
        var reloaded = AppConfig.Load();

        _config.ProcessName = reloaded.ProcessName;
        _config.OpenChatKey = reloaded.OpenChatKey;
        _config.SendChatKey = reloaded.SendChatKey;
        _config.TalkKey = reloaded.TalkKey;
        _config.ConfirmKey = reloaded.ConfirmKey;
        _config.CancelKey = reloaded.CancelKey;
        _config.ReviewBeforeSending = reloaded.ReviewBeforeSending;
        _config.KeyDelayMs = reloaded.KeyDelayMs;
        _config.ChatOpenDelayMs = reloaded.ChatOpenDelayMs;
        _config.BeforeSendDelayMs = reloaded.BeforeSendDelayMs;
        _config.MinSecondsBetweenMessages = reloaded.MinSecondsBetweenMessages;
        _config.MaxRecordingSeconds = reloaded.MaxRecordingSeconds;
        _config.MicrophoneDeviceIndex = reloaded.MicrophoneDeviceIndex;
        _config.SilenceThresholdRms = reloaded.SilenceThresholdRms;
        _config.WhisperModelFile = reloaded.WhisperModelFile;
        _config.Language = reloaded.Language;
        _config.MaxMessageLength = reloaded.MaxMessageLength;

        _config.PushToTalk = reloaded.PushToTalk;

        _hook.ConfirmKey = _config.ConfirmKey;
        _hook.CancelKey = _config.CancelKey;
        _controller.Binding = _config.PushToTalk ?? PttBinding.FromKey(_config.TalkKey);

        _watcher.ProcessName = _config.ProcessName;
        _recordingLimit.Interval = Math.Max(1, _config.MaxRecordingSeconds) * 1000;

        RefreshStatus();
        _ = LoadModelAsync();
    }

    // ------------------------------------------------------------------ Status

    private void ShowStatus(string text, bool isError = false)
    {
        _overlay.ShowStatus(text, isError);
        _statusTimeout.Stop();
        _statusTimeout.Start();
    }

    private void RefreshStatus()
    {
        if (_downloading)
        {
            return; // The download owns the icon and tooltip until it finishes.
        }

        bool ready = _watcher.IsRunning && _transcriber is not null && _hook.IsInstalled;

        string state = !_hook.IsInstalled ? "keyboard hook not installed"
            : _transcriber is null ? "no speech model"
            : !_watcher.IsRunning ? $"waiting for {_config.ProcessName}"
            : $"ready — hold {_controller.Binding.Describe()}";

        _statusItem.Text = state;
        _tray.Text = Truncate($"Gaggle — {state}", TrayTextLimit);
        _tray.Icon = TrayIcons.Create(ready ? TrayIcons.Ready : TrayIcons.Idle);
    }

    // ----------------------------------------------------------------- Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.Dispose();
            _recordingLimit.Dispose();
            _statusTimeout.Dispose();
            _recorder.Dispose();
            _transcriber?.Dispose();
            _sender.Dispose();
            _watcher.Dispose();
            _overlay.Dispose();

            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
