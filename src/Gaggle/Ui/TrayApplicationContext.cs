using System.Diagnostics;
using System.Windows.Forms;
using Gaggle.Audio;
using Gaggle.Condor;
using Gaggle.Configuration;
using Gaggle.Input;
using Gaggle.Interop;
using Gaggle.Net;
using Gaggle.Speech;
using Gaggle.Text;
using Gaggle.Update;

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
    private ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripMenuItem _handsFreeItem;
    private readonly ToolStripMenuItem _cuesItem;
    private readonly CondorWatcher _watcher;
    private readonly ChatSender _sender;
    private readonly MicrophoneRecorder _recorder = new();
    private readonly PushToTalkHook _hook = new();
    private readonly JoystickWatcher _joystick = new();
    private readonly PttController _controller;
    private readonly ReviewOverlay _overlay = new();
    private readonly System.Windows.Forms.Timer _recordingLimit;
    private readonly System.Windows.Forms.Timer _statusTimeout;
    private readonly System.Windows.Forms.Timer _updateCheckDelay;

    /// <summary>NotifyIcon.Text throws above this length.</summary>
    private const int TrayTextLimit = 63;

    /// <summary>The update menu item before a release has been found.</summary>
    /// <summary>
    /// How long after startup the background update check runs. Long enough that the
    /// tray icon is up and the model has started loading first.
    /// </summary>
    private const int UpdateCheckDelayMs = 5000;

    private WhisperTranscriber? _transcriber;
    private string? _pendingMessage;
    private bool _busy;
    private bool _downloading;

    /// <summary>A release newer than this one, once a check has found one.</summary>
    private ReleaseInfo? _availableUpdate;

    /// <summary>Set when the chosen language needs a model the chosen model is not.</summary>
    private bool _needsMultilingualModel;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();

        // Before anything reads a string: menus, forms and status lines all take
        // their text from UiText.Current as they are built.
        UiText.Use(_config.UiLanguage);

        _watcher = new CondorWatcher(_config.ProcessName);
        _sender = new ChatSender(_watcher);

        _statusItem = new ToolStripMenuItem(UiText.Current.StatusStarting) { Enabled = false };

        _updateItem = new ToolStripMenuItem(UiText.Current.MenuCheckForUpdates);
        _updateItem.Click += async (_, _) =>
        {
            // Once a release is known, the item is an offer rather than a question, so
            // clicking it should not go back to GitHub to be told the same thing.
            if (_availableUpdate is not null)
            {
                ShowUpdate();
                return;
            }

            await CheckForUpdatesAsync(silent: false);
        };

        _handsFreeItem = new ToolStripMenuItem(UiText.Current.MenuHandsFree);
        _handsFreeItem.Click += (_, _) => ToggleHandsFree();

        _cuesItem = new ToolStripMenuItem(UiText.Current.MenuAudibleCues);
        _cuesItem.Click += (_, _) =>
        {
            _config.AudibleFeedback = !_config.AudibleFeedback;
            _config.Save();

            // Turning them on says so out loud; there is nothing else to look at.
            Cue(CueTones.PlaySent);
        };

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

        // One-shot: the tray icon appears immediately and the check happens once the
        // startup rush is over.
        _updateCheckDelay = new System.Windows.Forms.Timer { Interval = UpdateCheckDelayMs };
        _updateCheckDelay.Tick += (_, _) =>
        {
            _updateCheckDelay.Stop();
            _ = CheckForUpdatesAsync(silent: true);
        };
        _updateCheckDelay.Start();

        _watcher.StateChanged += (_, _) => RefreshStatus();
        _watcher.Start();

        CueTones.Warm();

        _recorder.Failed += ex => BeginInvokeOnUi(() => UtteranceFailed(UiText.Current.MicrophoneError(ex.Message)));

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
                ex.Message + Environment.NewLine + Environment.NewLine + UiText.Current.HookFailedSuffix,
                "Gaggle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        RefreshStatus();
        _ = LoadModelAsync();

        // Deferred rather than shown from here: this runs before Application.Run, and a
        // modal dialog opened now would hold the message loop shut with no tray icon
        // behind it to say what the window belongs to.
        if (!_config.OnboardingSeen)
        {
            BeginInvokeOnUi(ShowOnboarding);
        }
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

        // The submenus rebuild their check marks on DropDownOpening; these two are
        // not rebuilt, so they read the config here for the same reason. Setting them
        // once at construction would go stale the moment anything else changed the
        // config - "Reload config", the settings window, a hand edit.
        menu.Opening += (_, _) =>
        {
            _handsFreeItem.Checked = !_config.ReviewBeforeSending;
            _cuesItem.Checked = _config.AudibleFeedback;
        };

        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        var microphones = new ToolStripMenuItem(UiText.Current.MenuMicrophone);
        microphones.DropDownOpening += (_, _) => PopulateMicrophones(microphones);
        menu.Items.Add(microphones);

        var models = new ToolStripMenuItem(UiText.Current.MenuSpeechModel);
        models.DropDownOpening += (_, _) => PopulateModels(models);
        menu.Items.Add(models);

        var languages = new ToolStripMenuItem(UiText.Current.MenuSpeechLanguage);
        languages.DropDownOpening += (_, _) => PopulateLanguages(languages);
        menu.Items.Add(languages);

        var display = new ToolStripMenuItem(UiText.Current.MenuDisplayLanguage);
        display.DropDownOpening += (_, _) => PopulateDisplayLanguages(display);
        menu.Items.Add(display);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(UiText.Current.MenuPushToTalk, null, (_, _) => ShowSettings());
        menu.Items.Add(_handsFreeItem);
        menu.Items.Add(_cuesItem);
        menu.Items.Add(UiText.Current.MenuOpenConfig, null, (_, _) => OpenConfig());
        menu.Items.Add(UiText.Current.MenuReloadConfig, null, (_, _) => ReloadConfig());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_updateItem);
        menu.Items.Add(UiText.Current.MenuReportBug, null, (_, _) => ReportIssue(IssueLink.BugTemplate));
        menu.Items.Add(UiText.Current.MenuSuggestIdea, null, (_, _) => ReportIssue(IssueLink.SuggestionTemplate));
        menu.Items.Add(UiText.Current.MenuGettingStarted, null, (_, _) => ShowOnboarding());
        menu.Items.Add(UiText.Current.MenuAbout(AppInfo.Name), null, (_, _) => ShowAbout());
        menu.Items.Add(UiText.Current.MenuExit, null, (_, _) => ExitThread());

        return menu;
    }

    // -------------------------------------------------------------- Model load

    private async Task LoadModelAsync()
    {
        if (!ModelInstaller.IsInstalled(_config.ModelPath))
        {
            // "No model at all" is the more useful thing to report, so clear any
            // stale language mismatch before the status line is redrawn.
            _needsMultilingualModel = false;
            RefreshStatus();
            _tray.ShowBalloonTip(
                8000,
                "Gaggle",
                UiText.Current.NoModelInstalledYet,
                ToolTipIcon.Info);
            return;
        }

        if (!ModelInstaller.IsMultilingualFile(_config.WhisperModelFile)
            && !SpokenLanguage.IsEnglish(_config.Language))
        {
            // An ".en" build contains English and nothing else, and has no translate
            // task at all. Loading it here would work; asking it for Polish would then
            // produce confident English nonsense with no error to explain it.
            _needsMultilingualModel = true;
            _transcriber?.Dispose();
            _transcriber = null;

            RefreshStatus();
            _tray.ShowBalloonTip(
                8000,
                "Gaggle",
                UiText.Current.NeedsMultilingualPickOne(SpokenLanguage.Describe(_config.Language)),
                ToolTipIcon.Warning);
            return;
        }

        _needsMultilingualModel = false;

        try
        {
            string modelPath = _config.ModelPath;

            WhisperTranscriber loaded = await Task.Run(() => WhisperTranscriber.Load(modelPath));

            _transcriber?.Dispose();
            _transcriber = loaded;
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(8000, "Gaggle", UiText.Current.CouldNotLoadModel(ex.Message), ToolTipIcon.Error);
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
            UtteranceFailed(UiText.Current.NoModelLoadedSeeMenu);
            return;
        }

        if (!_watcher.IsRunning)
        {
            UtteranceFailed(UiText.Current.NotRunning(_config.ProcessName));
            return;
        }

        DiscardPending();

        try
        {
            _recorder.Start(_config.MicrophoneDeviceIndex);
        }
        catch (Exception ex)
        {
            UtteranceFailed(UiText.Current.MicrophoneUnavailable(ex.Message));
            return;
        }

        // Set per recording rather than once, because hands-free can be switched on
        // between one utterance and the next.
        _recordingLimit.Interval = Math.Max(1, _config.ReviewBeforeSending
            ? _config.MaxRecordingSeconds
            : _config.HandsFreeMaxRecordingSeconds) * 1000;
        _recordingLimit.Start();

        _tray.Icon = TrayIcons.Create(TrayIcons.Recording);
        Cue(CueTones.PlayStarted);
        _overlay.ShowStatus(UiText.Current.Listening);
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
            UtteranceFailed(UiText.Current.NothingRecorded);
            RefreshStatus();
            return;
        }

        _overlay.ShowStatus(UiText.Current.Transcribing);
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
                UtteranceFailed(UiText.Current.TooQuiet);
                return;
            }

            WhisperTranscriber? transcriber = _transcriber;
            if (transcriber is null)
            {
                UtteranceFailed(UiText.Current.NoModelLoaded);
                return;
            }

            var options = new TranscriptionOptions
            {
                Language = _config.Language,
                Threads = _config.TranscriptionThreads,
                AudioContextSize = _config.FastTranscription
                    ? TranscriptionOptions.AudioContextFor(MicrophoneRecorder.CalculateDuration(audio))
                    : 0,
            };

            string raw = await transcriber.TranscribeAsync(audio, options);

            // Folded unconditionally: InputSender.TypeChar drops characters the layout
            // cannot produce without an error, and that bites whenever a non-English
            // word survives — translated or not. It is a no-op on English.
            string? message = MessageSanitiser.Clean(
                raw,
                _config.MaxMessageLength,
                foldToAscii: true);

            if (message is null)
            {
                UtteranceFailed(UiText.Current.DidNotCatchThat);
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
            UtteranceFailed(UiText.Current.TranscriptionFailed(ex.Message));
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

        _overlay.ShowStatus(UiText.Current.Sending);
        _ = SendAsync(message);
    }

    private async Task SendAsync(string message)
    {
        SendResult result = await _sender.SendAsync(message, _config);

        if (result.Success)
        {
            Cue(CueTones.PlaySent);
            _overlay.HideOverlay();
        }
        else if (result.Outcome == SendOutcome.UnsupportedCharacters)
        {
            // A failure on screen but not in Condor: ChatSender taps the send key and
            // stamps the rate limit before it reports the characters this layout could
            // not type. The message is in the chat, minus a few letters, so the tone has
            // to say sent - a drop tone would send someone hunting for a message that is
            // already there.
            Cue(CueTones.PlaySent);
            ShowStatus(result.Describe(), isError: true);
        }
        else
        {
            UtteranceFailed(result.Describe());
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

    private void PopulateDisplayLanguages(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        foreach (string code in UiLanguages.Codes)
        {
            string chosen = code;

            var item = new ToolStripMenuItem(UiLanguages.NameOf(code))
            {
                Checked = _config.UiLanguage == code,
            };

            item.Click += (_, _) =>
            {
                _config.UiLanguage = chosen;
                _config.Save();

                // Deferred: ApplyLanguage disposes the menu this click came from, and
                // doing that while it is still handling the click is asking for trouble.
                BeginInvokeOnUi(() => ApplyLanguage(chosen));
            };

            parent.DropDownItems.Add(item);
        }
    }

    /// <summary>
    /// Redraws the whole interface in another language.
    ///
    /// The menu is rebuilt rather than walked, because the submenus are built fresh on
    /// open anyway and half the items are created inside <see cref="BuildMenu"/>. The
    /// four items that outlive a rebuild are re-lettered by hand and taken out of the old
    /// menu before it is disposed, since disposing a menu disposes everything in it.
    /// </summary>
    private void ApplyLanguage(string languageCode)
    {
        UiText.Use(languageCode);

        _handsFreeItem.Text = UiText.Current.MenuHandsFree;
        _cuesItem.Text = UiText.Current.MenuAudibleCues;
        _updateItem.Text = _availableUpdate is null
            ? UiText.Current.MenuCheckForUpdates
            : UiText.Current.MenuUpdateTo(_availableUpdate.Version);

        ContextMenuStrip previous = _menu;

        previous.Items.Remove(_statusItem);
        previous.Items.Remove(_handsFreeItem);
        previous.Items.Remove(_cuesItem);
        previous.Items.Remove(_updateItem);

        _menu = BuildMenu();
        _tray.ContextMenuStrip = _menu;
        previous.Dispose();

        RefreshStatus();
    }

    private void PopulateMicrophones(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        IReadOnlyList<string> devices = MicrophoneRecorder.ListDevices();

        if (devices.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(UiText.Current.MenuNoMicrophones) { Enabled = false });
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

    /// <summary>
    /// Language is read fresh for every utterance rather than baked into the loaded
    /// model, so switching is instant — the only work is re-checking that the current
    /// model can actually speak the language just chosen.
    /// </summary>
    private void PopulateLanguages(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        foreach (SpokenLanguage language in SpokenLanguage.MenuChoices)
        {
            var item = new ToolStripMenuItem(language.Name)
            {
                // A config pinned to "pl" is still English-in-English-out as far as
                // this menu is concerned, so it checks the same row as "auto".
                Checked = SpokenLanguage.IsEnglish(language.Code)
                    == SpokenLanguage.IsEnglish(_config.Language),
            };

            item.Click += async (_, _) => await SelectLanguageAsync(language);
            parent.DropDownItems.Add(item);
        }
    }

    private async Task SelectLanguageAsync(SpokenLanguage language)
    {
        if (string.Equals(_config.Language, language.Code, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The language and the model are two halves of one decision, so choosing here
        // settles both. English wants a dedicated English build — smaller and faster
        // at English than the multilingual one — and anything else needs multilingual.
        bool settled = SpokenLanguage.IsEnglish(language.Code)
            ? await EnsureEnglishModelAsync()
            : await EnsureMultilingualModelAsync(language);

        if (!settled)
        {
            // Declined. Leaving the language alone keeps the app working, which is
            // what cancelling ought to mean.
            return;
        }

        _config.Language = language.Code;
        _config.Save();

        // No reload needed for the language itself; this re-runs the multilingual check
        // and puts the transcriber back if a previous mismatch had cleared it.
        await LoadModelAsync();
    }

    /// <summary>
    /// Points <see cref="AppConfig.WhisperModelFile"/> at an English-only build,
    /// downloading one if the user agrees. Returns false if they say no.
    /// </summary>
    private async Task<bool> EnsureEnglishModelAsync()
    {
        if (!ModelInstaller.IsMultilingualFile(_config.WhisperModelFile))
        {
            return true;
        }

        if (_downloading)
        {
            ShowStatus(UiText.Current.DownloadAlreadyRunning, isError: true);
            return false;
        }

        // Anything already on disk beats a download. Available is ordered smallest
        // first, so the last installed English build is the most capable one.
        ModelInstaller.ModelChoice? installed = ModelInstaller.Available.LastOrDefault(
            choice => !choice.IsMultilingual
                && File.Exists(Path.Combine(AppConfig.DataDirectory, choice.FileName)));

        if (installed is not null)
        {
            _config.WhisperModelFile = installed.FileName;
            ShowStatus(UiText.Current.SwitchedTo(installed.Name));
            return true;
        }

        ModelInstaller.ModelChoice offer = ModelInstaller.CounterpartEnglish(_config.WhisperModelFile);

        DialogResult answer = MessageBox.Show(
            UiText.Current.EnglishModelIsBetterAtEnglish
                + Environment.NewLine + Environment.NewLine
                + UiText.Current.DownloadNow(offer.Name, offer.Notes),
            "Gaggle",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (answer != DialogResult.OK)
        {
            return false;
        }

        if (!await DownloadModelAsync(offer, Path.Combine(AppConfig.DataDirectory, offer.FileName), alreadyConfirmed: true))
        {
            return false;
        }

        _config.WhisperModelFile = offer.FileName;
        return true;
    }

    /// <summary>
    /// Points <see cref="AppConfig.WhisperModelFile"/> at a model that can serve the
    /// given language, downloading one if the user agrees. Returns false if they say
    /// no, in which case nothing has changed.
    /// </summary>
    private async Task<bool> EnsureMultilingualModelAsync(SpokenLanguage language)
    {
        if (_downloading)
        {
            ShowStatus(UiText.Current.DownloadAlreadyRunning, isError: true);
            return false;
        }

        // Nobody should download half a gigabyte twice, so an installed multilingual
        // model wins over any download.
        ModelInstaller.ModelChoice? installed = ModelInstaller.Available.FirstOrDefault(
            choice => choice.IsMultilingual
                && File.Exists(Path.Combine(AppConfig.DataDirectory, choice.FileName)));

        if (installed is not null)
        {
            _config.WhisperModelFile = installed.FileName;
            ShowStatus(UiText.Current.SwitchedToFor(installed.Name, language.Name));
            return true;
        }

        ModelInstaller.ModelChoice? offer = ModelInstaller.Available.FirstOrDefault(
            choice => choice.IsMultilingual);

        if (offer is null)
        {
            return false;
        }

        DialogResult answer = MessageBox.Show(
            UiText.Current.LanguageNeedsMultilingual(language.Name)
                + Environment.NewLine + Environment.NewLine
                + UiText.Current.DownloadNow(offer.Name, offer.Notes),
            "Gaggle",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (answer != DialogResult.OK)
        {
            return false;
        }

        string destination = Path.Combine(AppConfig.DataDirectory, offer.FileName);

        if (!await DownloadModelAsync(offer, destination, alreadyConfirmed: true))
        {
            return false;
        }

        _config.WhisperModelFile = offer.FileName;
        return true;
    }

    private async Task SelectModelAsync(ModelInstaller.ModelChoice choice, bool installed)
    {
        if (_downloading)
        {
            ShowStatus(UiText.Current.DownloadAlreadyRunning, isError: true);
            return;
        }

        string destination = Path.Combine(AppConfig.DataDirectory, choice.FileName);

        if (!installed && !await DownloadModelAsync(choice, destination))
        {
            return;
        }

        _config.WhisperModelFile = choice.FileName;

        // The two menus are halves of one decision, so picking an English-only build
        // settles the language rather than leaving behind a pairing that refuses to
        // load. Between them the menus can no longer produce that state at all; the
        // check in LoadModelAsync now only catches a hand-edited config.
        if (!choice.IsMultilingual && !SpokenLanguage.IsEnglish(_config.Language))
        {
            _config.Language = SpokenLanguage.EnglishCode;
            ShowStatus(UiText.Current.EnglishOnlySoLanguageIsEnglish(choice.Name));
        }

        _config.Save();

        await LoadModelAsync();
    }

    /// <summary>
    /// Downloads a model, reporting progress in the overlay and the tray tooltip.
    /// These files run to hundreds of megabytes, so silence here reads as a hang.
    /// </summary>
    private async Task<bool> DownloadModelAsync(
        ModelInstaller.ModelChoice choice,
        string destination,
        bool alreadyConfirmed = false)
    {
        if (!alreadyConfirmed)
        {
            DialogResult answer = MessageBox.Show(
                UiText.Current.DownloadFromHuggingFace(choice.Name, choice.Notes),
                "Gaggle",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (answer != DialogResult.OK)
            {
                return false;
            }
        }

        // Constructed on the UI thread, so its callbacks arrive there too.
        var progress = new Progress<DownloadProgress>(report =>
        {
            string text = UiText.Current.Downloading(choice.Name, report.Describe());
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
            ShowStatus(UiText.Current.ModelInstalled(choice.Name));
            return true;
        }
        catch (Exception ex)
        {
            _overlay.HideOverlay();
            MessageBox.Show(UiText.Current.DownloadFailed(ex.Message), "Gaggle", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    // ---------------------------------------------------------------- Feedback

    /// <summary>
    /// Opens one of the GitHub issue forms in the browser, prefilled.
    ///
    /// Built on click rather than kept current, because the interesting part — the
    /// model, the language, whether Condor is up — is exactly what someone changes
    /// just before the thing they want to report.
    /// </summary>
    private void ReportIssue(string template)
    {
        // The suggestion form has no setup field, and GitHub ignores a parameter that
        // matches no field rather than saying so, so it is left off instead.
        string? environment = template == IssueLink.BugTemplate
            ? IssueLink.DescribeEnvironment(_config, DescribeMicrophone(), _watcher.IsRunning)
            : null;

        Open(IssueLink.For(template, environment));
    }

    /// <summary>
    /// The microphone as a person would name it. An index that no longer resolves is
    /// worth saying out loud: it is a plausible cause of "nothing was recorded".
    /// </summary>
    private string DescribeMicrophone()
    {
        if (_config.MicrophoneDeviceIndex < 0)
        {
            return "system default";
        }

        IReadOnlyList<string> devices = MicrophoneRecorder.ListDevices();

        return _config.MicrophoneDeviceIndex < devices.Count
            ? devices[_config.MicrophoneDeviceIndex]
            : $"index {_config.MicrophoneDeviceIndex} — no such device";
    }

    /// <summary>
    /// Opens the guide, remembering the language it was left in. Marked seen on the way
    /// out rather than the way in, so a crash while it is open does not cost a first-run
    /// user the one thing that explains the app.
    /// </summary>
    private void ShowOnboarding()
    {
        using var form = new OnboardingForm(_config.UiLanguage);
        form.ShowDialog();

        _config.OnboardingSeen = true;
        _config.Save();

        // ShowDialog has returned, so the menu this may have come from is closed and the
        // rebuild is safe to do here.
        if (_config.UiLanguage != form.LanguageCode)
        {
            _config.UiLanguage = form.LanguageCode;
            _config.Save();
            ApplyLanguage(form.LanguageCode);
        }
    }

    private static void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog();
    }

    private void ShowSettings()
    {
        // ShowDialog keeps pumping messages, so both the keyboard hook and the
        // joystick poll timer stay live and can capture a new binding.
        using var form = new SettingsForm(
            _controller,
            _controller.Binding,
            handsFree: !_config.ReviewBeforeSending,
            audibleCues: _config.AudibleFeedback);

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

        // Nothing to re-wire: ReviewBeforeSending is read fresh per utterance and the
        // tray ticks are recomputed when the menu next opens. Only a pending review has
        // to go, for the reason ToggleHandsFree explains.
        if (form.HandsFree == _config.ReviewBeforeSending)
        {
            _config.ReviewBeforeSending = !form.HandsFree;
            DiscardPending();
        }

        _config.AudibleFeedback = form.AudibleCues;

        _config.Save();
        RefreshStatus();
    }

    /// <summary>
    /// Flips hands-free, asking first when turning it on.
    ///
    /// The question matches the one asked before an English-only model is paired with a
    /// non-English language: both are choices that look small in a menu and are not.
    /// Turning it back off needs no ceremony.
    /// </summary>
    private void ToggleHandsFree()
    {
        if (_config.ReviewBeforeSending)
        {
            DialogResult answer = MessageBox.Show(
                UiText.Current.HandsFreeConfirm,
                "Gaggle",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.OK)
            {
                return;
            }
        }

        _config.ReviewBeforeSending = !_config.ReviewBeforeSending;

        // A transcript already waiting for Enter was composed under the old rules, and
        // the hook is still swallowing Enter and Escape for it. Left alone it would fire
        // into chat on the next Enter pressed in Condor, long after the overlay that
        // explained it has been forgotten.
        DiscardPending();

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
        _config.AudibleFeedback = reloaded.AudibleFeedback;
        _config.KeyDelayMs = reloaded.KeyDelayMs;
        _config.ChatOpenDelayMs = reloaded.ChatOpenDelayMs;
        _config.BeforeSendDelayMs = reloaded.BeforeSendDelayMs;
        _config.MinSecondsBetweenMessages = reloaded.MinSecondsBetweenMessages;
        _config.MaxRecordingSeconds = reloaded.MaxRecordingSeconds;
        _config.HandsFreeMaxRecordingSeconds = reloaded.HandsFreeMaxRecordingSeconds;
        _config.MicrophoneDeviceIndex = reloaded.MicrophoneDeviceIndex;
        _config.SilenceThresholdRms = reloaded.SilenceThresholdRms;
        _config.WhisperModelFile = reloaded.WhisperModelFile;
        _config.Language = reloaded.Language;
        _config.TranscriptionThreads = reloaded.TranscriptionThreads;
        _config.FastTranscription = reloaded.FastTranscription;
        _config.MaxMessageLength = reloaded.MaxMessageLength;
        _config.UiLanguage = reloaded.UiLanguage;
        _config.OnboardingSeen = reloaded.OnboardingSeen;
        _config.CheckForUpdates = reloaded.CheckForUpdates;
        _config.LastUpdateCheckUtc = reloaded.LastUpdateCheckUtc;
        _config.SkippedVersion = reloaded.SkippedVersion;

        _config.PushToTalk = reloaded.PushToTalk;

        _hook.ConfirmKey = _config.ConfirmKey;
        _hook.CancelKey = _config.CancelKey;
        _controller.Binding = _config.PushToTalk ?? PttBinding.FromKey(_config.TalkKey);

        _watcher.ProcessName = _config.ProcessName;

        RefreshStatus();
        _ = LoadModelAsync();

        // Deferred for the same reason as the language menu: this runs from a click on
        // the menu ApplyLanguage replaces.
        BeginInvokeOnUi(() => ApplyLanguage(_config.UiLanguage));
    }

    // ----------------------------------------------------------------- Updates

    /// <summary>
    /// Looks for a newer release. A background check is throttled and stays silent about
    /// anything that goes wrong; a check the user asked for always runs and always says
    /// something, because otherwise the menu item looks broken.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (silent && !UpdateCheck.ShouldCheckInBackground(
                _config.CheckForUpdates,
                _config.LastUpdateCheckUtc,
                DateTimeOffset.UtcNow))
        {
            return;
        }

        ReleaseInfo? release = await GitHubReleases.FetchLatestAsync();

        // Stamped whether or not the fetch worked, so a machine that is offline every
        // morning does not retry on every single start.
        _config.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
        _config.Save();

        if (release is null)
        {
            if (!silent)
            {
                MessageBox.Show(
                    UiText.Current.CouldNotReachGitHub,
                    "Gaggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        if (!UpdateCheck.ShouldOffer(AppInfo.Version, release.Version, _config.SkippedVersion, silent))
        {
            if (!silent)
            {
                MessageBox.Show(
                    UiText.Current.IsTheLatestVersion(AppInfo.Name, AppInfo.Version),
                    "Gaggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        _availableUpdate = release;

        // The balloon tip is gone in seconds; the menu item is how someone finds this
        // again an hour later.
        _updateItem.Text = UiText.Current.MenuUpdateTo(release.Version);

        if (silent)
        {
            _tray.ShowBalloonTip(
                8000,
                "Gaggle",
                UiText.Current.UpdateIsAvailable(AppInfo.Name, release.Version),
                ToolTipIcon.Info);

            return;
        }

        ShowUpdate();
    }

    private void ShowUpdate()
    {
        ReleaseInfo? release = _availableUpdate;

        if (release is null)
        {
            return;
        }

        // A release with no package or no published checksum, and an install folder that
        // cannot be written to, both come to the same thing: we can point at it, but we
        // cannot replace ourselves with it.
        bool inPlace = release.IsInstallable && UpdateInstaller.CanInstallInPlace();

        using var form = new UpdateForm(release, inPlace);

        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        switch (form.Choice)
        {
            case UpdateChoice.Install:
                _ = InstallUpdateAsync(release);
                break;

            case UpdateChoice.Skip:
                _config.SkippedVersion = release.Version;
                _config.Save();
                _availableUpdate = null;
                _updateItem.Text = UiText.Current.MenuCheckForUpdates;
                break;

            case UpdateChoice.OpenPage:
                Open(GitHubReleases.ReleasesPageUrl);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Downloads and stages the update, then exits so the swap script can take over.
    /// Shaped like <see cref="DownloadModelAsync"/> deliberately: same guard, same
    /// progress, same icon, so the two cannot run over each other.
    /// </summary>
    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        if (_downloading)
        {
            ShowStatus(UiText.Current.DownloadAlreadyRunning, isError: true);
            return;
        }

        // Restarting out from under a recording or an unsent review message would throw
        // it away, and the user would have no idea why.
        if (_busy || _recorder.IsRecording || _pendingMessage is not null)
        {
            ShowStatus(UiText.Current.FinishMessageBeforeUpdating, isError: true);
            return;
        }

        // Constructed on the UI thread, so its callbacks arrive there too.
        var progress = new Progress<DownloadProgress>(report =>
        {
            string text = UiText.Current.Downloading(release.Version, report.Describe());
            _overlay.ShowStatus(text);
            _tray.Text = Truncate($"Gaggle — {text}", TrayTextLimit);
        });

        _downloading = true;
        _busy = true;
        _statusTimeout.Stop();
        _tray.Icon = TrayIcons.Create(TrayIcons.Working);

        try
        {
            await UpdateInstaller.InstallAsync(release, progress);
        }
        catch (Exception ex)
        {
            _overlay.HideOverlay();
            MessageBox.Show(UiText.Current.UpdateFailed(ex.Message), "Gaggle", MessageBoxButtons.OK, MessageBoxIcon.Error);

            _downloading = false;
            _busy = false;
            RefreshStatus();
            return;
        }

        // The swap script is now waiting on this process to exit before it can replace
        // Gaggle.exe, so nothing is put back on the way out.
        _overlay.ShowStatus(UiText.Current.RestartingToFinish);
        ExitThread();
    }

    /// <summary>
    /// Hands a URL to the shell. A dead link should not take the tray icon down with it.
    /// </summary>
    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
        }
    }

    // ------------------------------------------------------------------ Status

    /// <summary>
    /// An utterance produced nothing, for whatever reason. Says so on screen, and out
    /// loud when cues are on - in a headset the tone is the only half that arrives.
    ///
    /// Utterances only. The download and update failures use <see cref="ShowStatus"/>
    /// directly: a tone means "what you just said went nowhere", and firing it at a menu
    /// click is how that stops meaning anything.
    /// </summary>
    private void UtteranceFailed(string text)
    {
        Cue(CueTones.PlayDropped);
        ShowStatus(text, isError: true);
    }

    /// <summary>
    /// Plays a cue if the user asked for them, and returns immediately either way. Read
    /// fresh from the config so there is no second copy of the setting to drift.
    /// </summary>
    private void Cue(Action tone)
    {
        if (_config.AudibleFeedback)
        {
            tone();
        }
    }

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

        string state = !_hook.IsInstalled ? UiText.Current.StatusHookNotInstalled
            : _needsMultilingualModel ? UiText.Current.StatusNeedsMultilingual(SpokenLanguage.Describe(_config.Language))
            : _transcriber is null ? UiText.Current.StatusNoModel
            : !_watcher.IsRunning ? UiText.Current.StatusWaitingFor(_config.ProcessName)
            : UiText.Current.StatusReady(_controller.Binding.Describe());

        _statusItem.Text = state;
        _tray.Text = Truncate($"{AppInfo.Name} {AppInfo.Version} — {state}", TrayTextLimit);
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
            _updateCheckDelay.Dispose();
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
