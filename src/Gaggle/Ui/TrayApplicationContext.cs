using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using Gaggle.Audio;
using Gaggle.Condor;
using Gaggle.Configuration;
using Gaggle.Input;
using Gaggle.Interop;
using Gaggle.Localisation;
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
    // Not readonly, and not built in the constructor: the whole menu is thrown away and
    // rebuilt when the interface language changes, and a ContextMenuStrip disposes the
    // items in it. See BuildMenu and RebuildMenu.
    private ContextMenuStrip _menu;
    private ToolStripMenuItem _statusItem;
    private ToolStripMenuItem _updateItem;
    private ToolStripMenuItem _handsFreeItem;
    private ToolStripMenuItem _cuesItem;
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

        // First, and before anything below builds a caption: every string in this class
        // reads Strings.Current, which starts out English.
        Strings.Use(_config.UiLanguage);

        _watcher = new CondorWatcher(_config.ProcessName);
        _sender = new ChatSender(_watcher);

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
        _recordingLimit.Tick += (_, _) => AbandonRecording();

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

        _recorder.Failed += ex => BeginInvokeOnUi(() => UtteranceFailed(Strings.Current.MicrophoneError(ex.Message)));

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
                Strings.Current.PushToTalkUnavailable(ex.Message),
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

    /// <summary>
    /// Builds the menu, items and captions together.
    ///
    /// Called again every time the interface language changes, so nothing here may
    /// survive across calls. The four items this class keeps a field for are created
    /// here on purpose: a ContextMenuStrip disposes its items, so one held over from the
    /// previous menu would be disposed out from under the new one.
    /// </summary>
    [MemberNotNull(
        nameof(_statusItem),
        nameof(_updateItem),
        nameof(_handsFreeItem),
        nameof(_cuesItem))]
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem(Strings.Current.Starting) { Enabled = false };

        _updateItem = new ToolStripMenuItem(Strings.Current.CheckForUpdates);
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

        _handsFreeItem = new ToolStripMenuItem(Strings.Current.HandsFreeItem);
        _handsFreeItem.Click += (_, _) => ToggleHandsFree();

        _cuesItem = new ToolStripMenuItem(Strings.Current.AudibleCuesItem);
        _cuesItem.Click += (_, _) =>
        {
            _config.AudibleFeedback = !_config.AudibleFeedback;
            _config.Save();

            // Turning them on says so out loud; there is nothing else to look at.
            Cue(CueTones.PlaySent);
        };

        // A rebuilt menu has to come back saying whatever the old one was saying.
        RefreshUpdateItem();

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

        var microphones = new ToolStripMenuItem(Strings.Current.Microphone);
        microphones.DropDownOpening += (_, _) => PopulateMicrophones(microphones);
        menu.Items.Add(microphones);

        var models = new ToolStripMenuItem(Strings.Current.SpeechModel);
        models.DropDownOpening += (_, _) => PopulateModels(models);
        menu.Items.Add(models);

        // Two language menus now sit next to each other, and they are not the same
        // decision. This one is what the pilot speaks into the microphone; the next is
        // what these menus are written in. Calling either of them just "Language" is how
        // that gets confused.
        var speech = new ToolStripMenuItem(Strings.Current.SpeechLanguage);
        speech.DropDownOpening += (_, _) => PopulateLanguages(speech);
        menu.Items.Add(speech);

        var interfaceLanguage = new ToolStripMenuItem(Strings.Current.AppLanguage);
        interfaceLanguage.DropDownOpening += (_, _) => PopulateAppLanguages(interfaceLanguage);
        menu.Items.Add(interfaceLanguage);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(Strings.Current.PushToTalkSettings, null, (_, _) => ShowSettings());
        menu.Items.Add(_handsFreeItem);
        menu.Items.Add(_cuesItem);
        menu.Items.Add(Strings.Current.OpenConfigFile, null, (_, _) => OpenConfig());
        menu.Items.Add(Strings.Current.ReloadConfigItem, null, (_, _) => ReloadConfig());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_updateItem);
        menu.Items.Add(
            Strings.Current.ReportABug, null, (_, _) => ReportIssue(IssueLink.BugTemplate));
        menu.Items.Add(
            Strings.Current.SuggestAnIdea, null, (_, _) => ReportIssue(IssueLink.SuggestionTemplate));
        menu.Items.Add(Strings.Current.AboutItem(AppInfo.Name), null, (_, _) => ShowAbout());
        menu.Items.Add(Strings.Current.Exit, null, (_, _) => ExitThread());

        return menu;
    }

    /// <summary>
    /// Throws the menu away and builds it again in the current language.
    ///
    /// Deferred to the next message on purpose. Both callers are reached from a menu
    /// item's Click handler, and disposing a drop-down from inside its own handler pulls
    /// the control out from under the code still dispatching that click.
    /// </summary>
    private void RebuildMenu() => BeginInvokeOnUi(() =>
    {
        Strings.Use(_config.UiLanguage);

        ContextMenuStrip stale = _menu;
        _menu = BuildMenu();
        _tray.ContextMenuStrip = _menu;
        stale.Dispose();

        RefreshStatus();
    });

    /// <summary>
    /// The update item is a question before a release has been found and an offer after,
    /// and a rebuilt menu has to come back saying the right one.
    /// </summary>
    private void RefreshUpdateItem() =>
        _updateItem.Text = _availableUpdate is null
            ? Strings.Current.CheckForUpdates
            : Strings.Current.UpdateTo(_availableUpdate.Version);

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
                Strings.Current.NoModelInstalledBalloon,
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
                Strings.Current.NeedsMultilingualModelBalloon(
                    Strings.Current.SpokenLanguageName(_config.Language)),
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
            _tray.ShowBalloonTip(8000, "Gaggle", Strings.Current.CouldNotLoadModel(ex.Message), ToolTipIcon.Error);
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
            UtteranceFailed(Strings.Current.NoModelLoadedSeeMenu);
            return;
        }

        if (!_watcher.IsRunning)
        {
            UtteranceFailed(Strings.Current.ProcessNotRunning(_config.ProcessName));
            return;
        }

        DiscardPending();

        try
        {
            _recorder.Start(_config.MicrophoneDeviceIndex);
        }
        catch (Exception ex)
        {
            UtteranceFailed(Strings.Current.MicrophoneUnavailable(ex.Message));
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
        _overlay.ShowStatus(Strings.Current.Listening);
        _statusTimeout.Stop();
    }

    /// <summary>
    /// The hold ran past its limit, so the recording is thrown away instead of sent.
    ///
    /// This is the cancel gesture: keep holding and nothing reaches chat. It matters most
    /// hands-free, where there is no overlay to read and no Escape to aim at it — holding
    /// on is the only way to take back something you have already started saying.
    ///
    /// The watchdog used to transcribe and send what it stopped. That was the wrong half
    /// of the trade: a message cut off mid-sentence is worth less than being able to
    /// abandon one, and hands-free had no way to abandon anything at all.
    ///
    /// The key is still held when this fires. Releasing it re-enters
    /// <see cref="StopRecordingAndTranscribe"/>, which finds the recorder already stopped
    /// and does nothing — so the gesture ends here, not on release.
    /// </summary>
    private void AbandonRecording()
    {
        _recordingLimit.Stop();

        if (!_recorder.IsRecording)
        {
            return;
        }

        // Disposed rather than kept: nothing downstream will ever look at it, and these
        // buffers are 16 kHz mono for as long as the limit allows.
        using MemoryStream? audio = _recorder.Stop();

        UtteranceFailed(Strings.Current.HeldTooLong);
        RefreshStatus();
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
            UtteranceFailed(Strings.Current.NothingRecorded);
            RefreshStatus();
            return;
        }

        _overlay.ShowStatus(Strings.Current.Transcribing);
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
                UtteranceFailed(Strings.Current.TooQuiet);
                return;
            }

            WhisperTranscriber? transcriber = _transcriber;
            if (transcriber is null)
            {
                UtteranceFailed(Strings.Current.NoModelLoaded);
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
                UtteranceFailed(Strings.Current.DidNotCatchThat);
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
            UtteranceFailed(Strings.Current.TranscriptionFailed(ex.Message));
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

        _overlay.ShowStatus(Strings.Current.Sending);
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

    private void PopulateMicrophones(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        IReadOnlyList<string> devices = MicrophoneRecorder.ListDevices();

        if (devices.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.Current.NoMicrophonesFound) { Enabled = false });
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

            string caption =
                $"{Strings.Current.ModelName(choice.FileName)} — {Strings.Current.ModelNotes(choice.FileName)}";

            var item = new ToolStripMenuItem(caption)
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
            var item = new ToolStripMenuItem(Strings.Current.SpokenLanguageName(language.Code))
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

    /// <summary>
    /// The language the menus themselves are written in. Nothing here touches speech, so
    /// there is no model to check and no reload to do — only the menu to redraw.
    /// </summary>
    private void PopulateAppLanguages(ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();

        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            var item = new ToolStripMenuItem(Strings.NameOf(language))
            {
                Checked = _config.UiLanguage == language,
            };

            item.Click += (_, _) => SelectAppLanguage(language);
            parent.DropDownItems.Add(item);
        }
    }

    private void SelectAppLanguage(UiLanguage language)
    {
        if (_config.UiLanguage == language)
        {
            return;
        }

        _config.UiLanguage = language;
        _config.Save();

        RebuildMenu();
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
            ShowStatus(Strings.Current.ModelDownloadInProgress, isError: true);
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
            ShowStatus(Strings.Current.SwitchedToModel(Strings.Current.ModelName(installed.FileName)));
            return true;
        }

        ModelInstaller.ModelChoice offer = ModelInstaller.CounterpartEnglish(_config.WhisperModelFile);

        DialogResult answer = MessageBox.Show(
            Strings.Current.EnglishModelOffer(
                Strings.Current.ModelName(offer.FileName),
                Strings.Current.ModelNotes(offer.FileName)),
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
            ShowStatus(Strings.Current.ModelDownloadInProgress, isError: true);
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
            ShowStatus(Strings.Current.SwitchedToModelFor(
                Strings.Current.ModelName(installed.FileName),
                Strings.Current.SpokenLanguageName(language.Code)));
            return true;
        }

        ModelInstaller.ModelChoice? offer = ModelInstaller.Available.FirstOrDefault(
            choice => choice.IsMultilingual);

        if (offer is null)
        {
            return false;
        }

        DialogResult answer = MessageBox.Show(
            Strings.Current.MultilingualModelOffer(
                Strings.Current.SpokenLanguageName(language.Code),
                Strings.Current.ModelName(offer.FileName),
                Strings.Current.ModelNotes(offer.FileName)),
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
            ShowStatus(Strings.Current.ModelDownloadInProgress, isError: true);
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
            ShowStatus(Strings.Current.ModelIsEnglishOnly(Strings.Current.ModelName(choice.FileName)));
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
                Strings.Current.DownloadModelQuestion(
                    Strings.Current.ModelName(choice.FileName),
                    Strings.Current.ModelNotes(choice.FileName)),
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
            string text = Strings.Current.DownloadingModel(
                Strings.Current.ModelName(choice.FileName),
                report.Describe());

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
            ShowStatus(Strings.Current.ModelInstalled(Strings.Current.ModelName(choice.FileName)));
            return true;
        }
        catch (Exception ex)
        {
            _overlay.HideOverlay();
            MessageBox.Show(
                Strings.Current.DownloadFailed(ex.Message),
                "Gaggle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
                Strings.Current.HandsFreeWarning,
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
        _config.CheckForUpdates = reloaded.CheckForUpdates;
        _config.LastUpdateCheckUtc = reloaded.LastUpdateCheckUtc;
        _config.SkippedVersion = reloaded.SkippedVersion;
        _config.UiLanguage = reloaded.UiLanguage;

        _config.PushToTalk = reloaded.PushToTalk;

        _hook.ConfirmKey = _config.ConfirmKey;
        _hook.CancelKey = _config.CancelKey;
        _controller.Binding = _config.PushToTalk ?? PttBinding.FromKey(_config.TalkKey);

        _watcher.ProcessName = _config.ProcessName;

        // Rebuilt unconditionally rather than only when UiLanguage changed: the menu is
        // cheap, and this is the one path where a hand-edited file can have moved
        // anything the captions are built from.
        RebuildMenu();

        RefreshStatus();
        _ = LoadModelAsync();
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
                    Strings.Current.CouldNotReachGitHub,
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
                    Strings.Current.AlreadyLatest(AppInfo.Name, AppInfo.Version),
                    "Gaggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            return;
        }

        _availableUpdate = release;

        // The balloon tip is gone in seconds; the menu item is how someone finds this
        // again an hour later.
        RefreshUpdateItem();

        if (silent)
        {
            _tray.ShowBalloonTip(
                8000,
                "Gaggle",
                Strings.Current.UpdateAvailableBalloon(AppInfo.Name, release.Version),
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
                RefreshUpdateItem();
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
            ShowStatus(Strings.Current.DownloadInProgress, isError: true);
            return;
        }

        // Restarting out from under a recording or an unsent review message would throw
        // it away, and the user would have no idea why.
        if (_busy || _recorder.IsRecording || _pendingMessage is not null)
        {
            ShowStatus(Strings.Current.FinishMessageBeforeUpdating, isError: true);
            return;
        }

        // Constructed on the UI thread, so its callbacks arrive there too.
        var progress = new Progress<DownloadProgress>(report =>
        {
            string text = Strings.Current.DownloadingUpdate(release.Version, report.Describe());
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
            MessageBox.Show(
                Strings.Current.UpdateFailed(ex.Message),
                "Gaggle",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _downloading = false;
            _busy = false;
            RefreshStatus();
            return;
        }

        // The swap script is now waiting on this process to exit before it can replace
        // Gaggle.exe, so nothing is put back on the way out.
        _overlay.ShowStatus(Strings.Current.RestartingToFinishUpdate);
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

        string state = !_hook.IsInstalled ? Strings.Current.StatusHookNotInstalled
            : _needsMultilingualModel ? Strings.Current.StatusNeedsMultilingualModel(
                Strings.Current.SpokenLanguageName(_config.Language))
            : _transcriber is null ? Strings.Current.StatusNoSpeechModel
            : !_watcher.IsRunning ? Strings.Current.StatusWaitingFor(_config.ProcessName)
            : Strings.Current.StatusReady(_controller.Binding.Describe());

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
