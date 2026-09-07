namespace Gaggle.Ui;

/// <summary>
/// Every string the interface shows, in one language.
///
/// Members are <c>required</c> rather than positional, so the compiler enforces two
/// things at once: adding a string fails the build until all four languages have it, and
/// a parameterised string is a delegate, so a translation that forgets an argument - or
/// takes the wrong one - does not compile either. A resource file would fall back to
/// English silently on the first and not check the second at all.
///
/// Key names are deliberately absent. <see cref="Gaggle.Input.PttBinding"/> names the
/// keycap a pilot is looking at, and "Caps Lock" is what is printed on it whatever
/// language they read.
/// </summary>
public sealed record UiText
{
    // ---------------------------------------------------------------- Tray menu

    public required string MenuMicrophone { get; init; }

    public required string MenuNoMicrophones { get; init; }

    public required string MenuSpeechModel { get; init; }

    public required string MenuSpeechLanguage { get; init; }

    public required string MenuDisplayLanguage { get; init; }

    public required string MenuPushToTalk { get; init; }

    public required string MenuHandsFree { get; init; }

    public required string MenuAudibleCues { get; init; }

    public required string MenuOpenConfig { get; init; }

    public required string MenuReloadConfig { get; init; }

    public required string MenuCheckForUpdates { get; init; }

    public required Func<string, string> MenuUpdateTo { get; init; }

    public required string MenuReportBug { get; init; }

    public required string MenuSuggestIdea { get; init; }

    public required string MenuGettingStarted { get; init; }

    public required Func<string, string> MenuAbout { get; init; }

    public required string MenuExit { get; init; }

    // ------------------------------------------------------------- Tray status

    public required string StatusStarting { get; init; }

    public required string StatusHookNotInstalled { get; init; }

    public required Func<string, string> StatusNeedsMultilingual { get; init; }

    public required string StatusNoModel { get; init; }

    public required Func<string, string> StatusWaitingFor { get; init; }

    public required Func<string, string> StatusReady { get; init; }

    // ---------------------------------------------------------- One utterance

    public required string Listening { get; init; }

    public required string Transcribing { get; init; }

    public required string Sending { get; init; }

    public required Func<string, string> MicrophoneError { get; init; }

    public required string NoModelLoadedSeeMenu { get; init; }

    public required Func<string, string> NotRunning { get; init; }

    public required Func<string, string> MicrophoneUnavailable { get; init; }

    public required string NothingRecorded { get; init; }

    public required string TooQuiet { get; init; }

    public required string NoModelLoaded { get; init; }

    public required string DidNotCatchThat { get; init; }

    public required Func<string, string> TranscriptionFailed { get; init; }

    // ------------------------------------------------------ Models, languages

    public required string DownloadAlreadyRunning { get; init; }

    public required Func<string, string> SwitchedTo { get; init; }

    public required Func<string, string, string> SwitchedToFor { get; init; }

    public required Func<string, string> EnglishOnlySoLanguageIsEnglish { get; init; }

    public required Func<string, string> ModelInstalled { get; init; }

    public required Func<string, string, string> Downloading { get; init; }

    public required Func<string, string, string> DownloadFromHuggingFace { get; init; }

    public required Func<string, string> DownloadFailed { get; init; }

    public required Func<string, string, string> DownloadNow { get; init; }

    public required string EnglishModelIsBetterAtEnglish { get; init; }

    public required Func<string, string> LanguageNeedsMultilingual { get; init; }

    public required string NoModelInstalledYet { get; init; }

    public required Func<string, string> NeedsMultilingualPickOne { get; init; }

    public required Func<string, string> CouldNotLoadModel { get; init; }

    // ------------------------------------------------------------------ Update

    public required string FinishMessageBeforeUpdating { get; init; }

    public required string RestartingToFinish { get; init; }

    public required string CouldNotReachGitHub { get; init; }

    public required Func<string, string, string> IsTheLatestVersion { get; init; }

    public required Func<string, string, string> UpdateIsAvailable { get; init; }

    public required Func<string, string> UpdateFailed { get; init; }

    public required Func<string, string> UpdateWindowTitle { get; init; }

    public required Func<string, string> UpdateHeading { get; init; }

    public required Func<string, string> UpdateWillRestart { get; init; }

    public required Func<string, string> UpdateCannotSelfInstall { get; init; }

    public required string UpdateViewOnGitHub { get; init; }

    public required string UpdateSkipVersion { get; init; }

    public required string UpdateInstallNow { get; init; }

    public required string UpdateOpenDownloads { get; init; }

    public required string UpdateLater { get; init; }

    public required string UpdateNoNotes { get; init; }

    // ---------------------------------------------------------------- Dialogs

    public required string AlreadyRunning { get; init; }

    public required string HookFailedSuffix { get; init; }

    public required string HandsFreeConfirm { get; init; }

    // --------------------------------------------------------- Settings window

    public required string SettingsTitle { get; init; }

    public required string SettingsPushToTalkTitle { get; init; }

    public required string SettingsChange { get; init; }

    public required string SettingsStop { get; init; }

    public required string SettingsPressAKey { get; init; }

    public required string SettingsCapturing { get; init; }

    public required string SettingsKeyHidden { get; init; }

    public required string SettingsButtonNotHidden { get; init; }

    public required string SettingsJoystickWarning { get; init; }

    public required string SettingsDetectedDevices { get; init; }

    public required string SettingsNoJoysticks { get; init; }

    public required string SettingsSendingTitle { get; init; }

    public required string SettingsHandsFree { get; init; }

    public required string SettingsHandsFreeOn { get; init; }

    public required string SettingsHandsFreeOff { get; init; }

    public required string SettingsAudibleCues { get; init; }

    public required string SettingsOk { get; init; }

    public required string SettingsCancel { get; init; }

    // ------------------------------------------------------------ About window

    public required Func<string, string> AboutTitle { get; init; }

    public required Func<string, string> AboutByAuthor { get; init; }

    public required string AboutReportLink { get; init; }

    public required string AboutDataFolder { get; init; }

    public required string AboutClose { get; init; }

    // ----------------------------------------------------------- Send outcomes

    public required string SendSent { get; init; }

    public required string SendCondorNotRunning { get; init; }

    public required string SendCondorNotFocused { get; init; }

    public required string SendRateLimited { get; init; }

    public required string SendNothingToSend { get; init; }

    public required string SendBusy { get; init; }

    public required Func<string, string> SendUnsupportedCharacters { get; init; }

    public required string SendInjectionBlocked { get; init; }

    public required string SendUnknown { get; init; }

    // ---------------------------------------------------------------- Plumbing

    /// <summary>
    /// The language everything is currently drawn in. Backed by a field rather than an
    /// auto-property because a property initialiser here would run before the language
    /// tables below it and quietly assign null.
    ///
    /// Set once at startup from config
    /// and again when the display language changes; forms read it as they are built and
    /// the tray rebuilds its menu, so nothing has to observe it.
    /// </summary>
    private static UiText? _current;

    public static UiText Current => _current ??= English;

    public static void Use(string? languageCode) => _current = For(languageCode);

    /// <summary>The interface in one language, falling back to English.</summary>
    public static UiText For(string? languageCode) => UiLanguages.Normalise(languageCode) switch
    {
        "pl" => Polish,
        "de" => German,
        "es" => Spanish,
        _ => English,
    };

    public static UiText English { get; } = new()
    {
        MenuMicrophone = "Microphone",
        MenuNoMicrophones = "No microphones found",
        MenuSpeechModel = "Speech model",
        MenuSpeechLanguage = "Speech language",
        MenuDisplayLanguage = "Display language",
        MenuPushToTalk = "Push-to-talk…",
        MenuHandsFree = "Send without review (hands-free)",
        MenuAudibleCues = "Play audible cues",
        MenuOpenConfig = "Open config file",
        MenuReloadConfig = "Reload config",
        MenuCheckForUpdates = "Check for updates…",
        MenuUpdateTo = v => $"Update to {v}…",
        MenuReportBug = "Report a bug…",
        MenuSuggestIdea = "Suggest an idea…",
        MenuGettingStarted = "Getting started…",
        MenuAbout = n => $"About {n}…",
        MenuExit = "Exit",

        StatusStarting = "Starting…",
        StatusHookNotInstalled = "keyboard hook not installed",
        StatusNeedsMultilingual = l => $"{l} needs a multilingual model",
        StatusNoModel = "no speech model",
        StatusWaitingFor = p => $"waiting for {p}",
        StatusReady = b => $"ready — hold {b}",

        Listening = "Listening…",
        Transcribing = "Transcribing…",
        Sending = "Sending…",
        MicrophoneError = m => $"Microphone error: {m}",
        NoModelLoadedSeeMenu = "No speech model loaded — see the tray menu.",
        NotRunning = p => $"{p} is not running.",
        MicrophoneUnavailable = m => $"Microphone unavailable: {m}",
        NothingRecorded = "Nothing recorded.",
        TooQuiet = "Too quiet — nothing sent.",
        NoModelLoaded = "No speech model loaded.",
        DidNotCatchThat = "Did not catch that.",
        TranscriptionFailed = m => $"Transcription failed: {m}",

        DownloadAlreadyRunning = "A download is already running.",
        SwitchedTo = n => $"Switched to {n}.",
        SwitchedToFor = (n, l) => $"Switched to {n} for {l}.",
        EnglishOnlySoLanguageIsEnglish = n => $"{n} is English only — language set to English.",
        ModelInstalled = n => $"{n} installed.",
        Downloading = (n, p) => $"Downloading {n} — {p}",
        DownloadFromHuggingFace = (n, notes) => $"Download {n} ({notes}) from Hugging Face?",
        DownloadFailed = m => $"Download failed: {m}",
        DownloadNow = (n, notes) => $"Download {n} ({notes}) now?",
        EnglishModelIsBetterAtEnglish = "English uses a dedicated English speech model, which is "
            + "smaller and faster at English than the multilingual one.",
        LanguageNeedsMultilingual = l => $"{l} needs a multilingual speech model. The English-only "
            + "models cannot transcribe it — Whisper ignores the request and writes down what it "
            + "heard as English instead.",
        NoModelInstalledYet = "No speech model installed yet. Pick one from the tray menu under "
            + "Speech model.",
        NeedsMultilingualPickOne = l => $"{l} needs a multilingual model. Pick Small or Medium "
            + "(multilingual) under Speech model.",
        CouldNotLoadModel = m => $"Could not load the speech model: {m}",

        FinishMessageBeforeUpdating = "Finish the current message before updating.",
        RestartingToFinish = "Restarting to finish the update…",
        CouldNotReachGitHub = "Could not reach GitHub to check for updates.",
        IsTheLatestVersion = (n, v) => $"{n} {v} is the latest version.",
        UpdateIsAvailable = (n, v) => $"{n} {v} is available. Open the tray menu to install it.",
        UpdateFailed = m => $"Update failed: {m}",
        UpdateWindowTitle = n => $"Update {n}",
        UpdateHeading = nv => $"{nv} is available",
        UpdateWillRestart = v => $"You are running {v}. Gaggle will restart to finish.",
        UpdateCannotSelfInstall = v => $"You are running {v}. This copy cannot update itself.",
        UpdateViewOnGitHub = "View this release on GitHub",
        UpdateSkipVersion = "Skip this version",
        UpdateInstallNow = "Install now",
        UpdateOpenDownloads = "Open downloads",
        UpdateLater = "Later",
        UpdateNoNotes = "No release notes were published.",

        AlreadyRunning = "Gaggle is already running — look for it in the notification area.",
        HookFailedSuffix = "Push-to-talk will not work. Gaggle will keep running so you can "
            + "check settings.",
        HandsFreeConfirm = "Hands-free sends every transcript straight to chat, mishearings "
            + "included, with no chance to read it first."
            + "\n\nIt exists for VR, where the review overlay cannot be seen or answered. On a "
            + "monitor you are giving up the only human check on what other pilots receive."
            + "\n\nTurn it on?",

        SettingsTitle = "Gaggle settings",
        SettingsPushToTalkTitle = "Push-to-talk",
        SettingsChange = "Change…",
        SettingsStop = "Stop",
        SettingsPressAKey = "Press a key or button…",
        SettingsCapturing = "Listening. Every keystroke is captured, so use the Stop button with "
            + "the mouse if you change your mind.",
        SettingsKeyHidden = "This key is hidden from Condor while Gaggle is running.",
        SettingsButtonNotHidden = "Joystick buttons cannot be hidden from Condor.",
        SettingsJoystickWarning = "Condor will still see this button. Pick one the sim does not use, or it "
            + "will do both things at once.",
        SettingsDetectedDevices = "Detected devices",
        SettingsNoJoysticks = "No joysticks detected — keyboard only.",
        SettingsSendingTitle = "Sending",
        SettingsHandsFree = "Hands-free — send without reviewing",
        SettingsHandsFreeOn = "Whatever is transcribed goes straight into chat, mishearings "
            + "included, with nothing to read or discard first. Recordings are also cut shorter "
            + "than usual, because nobody is watching what the time limit sends.",
        SettingsHandsFreeOff = "Every message waits in the overlay first: Enter sends it, Escape "
            + "discards it. In VR that overlay cannot be seen or answered — that is what "
            + "hands-free is for.",
        SettingsAudibleCues = "Play a tone when recording starts, sends, or is dropped",
        SettingsOk = "OK",
        SettingsCancel = "Cancel",

        AboutTitle = n => $"About {n}",
        AboutByAuthor = a => $"by {a}",
        AboutReportLink = "Report a bug or suggest an idea",
        AboutDataFolder = "Settings and speech models:",
        AboutClose = "Close",

        SendSent = "Message sent.",
        SendCondorNotRunning = "Condor is not running.",
        SendCondorNotFocused = "Condor is not the active window.",
        SendRateLimited = "Slow down — rate limit active.",
        SendNothingToSend = "Nothing to send.",
        SendBusy = "Still sending the previous message.",
        SendUnsupportedCharacters = c => $"Cannot type on this keyboard layout: {c}",
        SendInjectionBlocked = "Windows blocked the keystrokes. Condor is probably running as "
            + "administrator — run Gaggle as administrator too.",
        SendUnknown = "Unknown result.",
    };

    public static UiText Polish { get; } = new()
    {
        MenuMicrophone = "Mikrofon",
        MenuNoMicrophones = "Nie znaleziono mikrofonów",
        MenuSpeechModel = "Model mowy",
        MenuSpeechLanguage = "Język mowy",
        MenuDisplayLanguage = "Język interfejsu",
        MenuPushToTalk = "Push-to-talk…",
        MenuHandsFree = "Wysyłaj bez sprawdzania (hands-free)",
        MenuAudibleCues = "Sygnały dźwiękowe",
        MenuOpenConfig = "Otwórz plik konfiguracji",
        MenuReloadConfig = "Wczytaj konfigurację ponownie",
        MenuCheckForUpdates = "Sprawdź aktualizacje…",
        MenuUpdateTo = v => $"Zaktualizuj do {v}…",
        MenuReportBug = "Zgłoś błąd…",
        MenuSuggestIdea = "Zaproponuj zmianę…",
        MenuGettingStarted = "Pierwsze kroki…",
        MenuAbout = n => $"O programie {n}…",
        MenuExit = "Zakończ",

        StatusStarting = "Uruchamianie…",
        StatusHookNotInstalled = "hak klawiatury nie zainstalowany",
        StatusNeedsMultilingual = l => $"{l} wymaga modelu wielojęzycznego",
        StatusNoModel = "brak modelu mowy",
        StatusWaitingFor = p => $"czekam na {p}",
        StatusReady = b => $"gotowe — przytrzymaj {b}",

        Listening = "Słucham…",
        Transcribing = "Rozpoznaję…",
        Sending = "Wysyłam…",
        MicrophoneError = m => $"Błąd mikrofonu: {m}",
        NoModelLoadedSeeMenu = "Nie wczytano modelu mowy — sprawdź menu w zasobniku.",
        NotRunning = p => $"{p} nie jest uruchomiony.",
        MicrophoneUnavailable = m => $"Mikrofon niedostępny: {m}",
        NothingRecorded = "Nic nie nagrano.",
        TooQuiet = "Za cicho — nic nie wysłano.",
        NoModelLoaded = "Nie wczytano modelu mowy.",
        DidNotCatchThat = "Nie zrozumiałem.",
        TranscriptionFailed = m => $"Rozpoznawanie nie powiodło się: {m}",

        DownloadAlreadyRunning = "Pobieranie już trwa.",
        SwitchedTo = n => $"Przełączono na {n}.",
        SwitchedToFor = (n, l) => $"Przełączono na {n} dla języka {l}.",
        EnglishOnlySoLanguageIsEnglish = n => $"{n} obsługuje tylko angielski — ustawiono język angielski.",
        ModelInstalled = n => $"Zainstalowano {n}.",
        Downloading = (n, p) => $"Pobieram {n} — {p}",
        DownloadFromHuggingFace = (n, notes) => $"Pobrać {n} ({notes}) z Hugging Face?",
        DownloadFailed = m => $"Pobieranie nie powiodło się: {m}",
        DownloadNow = (n, notes) => $"Pobrać teraz {n} ({notes})?",
        EnglishModelIsBetterAtEnglish = "Dla angielskiego używany jest dedykowany model angielski: "
            + "jest mniejszy i szybszy niż wielojęzyczny.",
        LanguageNeedsMultilingual = l => $"{l} wymaga wielojęzycznego modelu mowy. Modele tylko "
            + "angielskie tego nie potrafią — Whisper ignoruje prośbę i zapisuje to, co usłyszał, "
            + "jako angielski.",
        NoModelInstalledYet = "Nie zainstalowano jeszcze żadnego modelu mowy. Wybierz jeden w menu "
            + "w zasobniku, w „Model mowy”.",
        NeedsMultilingualPickOne = l => $"{l} wymaga modelu wielojęzycznego. Wybierz Small albo "
            + "Medium (multilingual) w „Model mowy”.",
        CouldNotLoadModel = m => $"Nie udało się wczytać modelu mowy: {m}",

        FinishMessageBeforeUpdating = "Dokończ bieżącą wiadomość przed aktualizacją.",
        RestartingToFinish = "Restartuję, aby dokończyć aktualizację…",
        CouldNotReachGitHub = "Nie udało się połączyć z GitHubem, aby sprawdzić aktualizacje.",
        IsTheLatestVersion = (n, v) => $"{n} {v} to najnowsza wersja.",
        UpdateIsAvailable = (n, v) => $"Dostępna jest wersja {n} {v}. Otwórz menu w zasobniku, aby ją zainstalować.",
        UpdateFailed = m => $"Aktualizacja nie powiodła się: {m}",
        UpdateWindowTitle = n => $"Aktualizacja {n}",
        UpdateHeading = nv => $"Dostępna jest wersja {nv}",
        UpdateWillRestart = v => $"Masz wersję {v}. Gaggle uruchomi się ponownie, aby dokończyć.",
        UpdateCannotSelfInstall = v => $"Masz wersję {v}. Ta kopia nie może zaktualizować się sama.",
        UpdateViewOnGitHub = "Zobacz to wydanie na GitHubie",
        UpdateSkipVersion = "Pomiń tę wersję",
        UpdateInstallNow = "Zainstaluj teraz",
        UpdateOpenDownloads = "Otwórz pobrane",
        UpdateLater = "Później",
        UpdateNoNotes = "Nie opublikowano informacji o wydaniu.",

        AlreadyRunning = "Gaggle jest już uruchomione — poszukaj go w zasobniku systemowym.",
        HookFailedSuffix = "Push-to-talk nie będzie działać. Gaggle pozostanie uruchomione, "
            + "żebyś mógł sprawdzić ustawienia.",
        HandsFreeConfirm = "Hands-free wysyła każdą transkrypcję prosto na czat, razem z "
            + "przesłyszeniami i bez możliwości przeczytania jej wcześniej."
            + "\n\nTen tryb jest dla VR, gdzie paska podglądu nie widać i nie da się na niego "
            + "odpowiedzieć. Na monitorze rezygnujesz z jedynej ludzkiej kontroli nad tym, co "
            + "dostaną inni piloci."
            + "\n\nWłączyć?",

        SettingsTitle = "Ustawienia Gaggle",
        SettingsPushToTalkTitle = "Push-to-talk",
        SettingsChange = "Zmień…",
        SettingsStop = "Zatrzymaj",
        SettingsPressAKey = "Naciśnij klawisz lub przycisk…",
        SettingsCapturing = "Nasłuchuję. Każde naciśnięcie klawisza jest przechwytywane, więc "
            + "jeśli zmienisz zdanie, użyj myszy i przycisku Zatrzymaj.",
        SettingsKeyHidden = "Ten klawisz jest ukrywany przed Condorem, gdy Gaggle działa.",
        SettingsButtonNotHidden = "Przycisków joysticka nie da się ukryć przed Condorem.",
        SettingsJoystickWarning = "Condor i tak zobaczy ten przycisk. Wybierz taki, którego symulator nie "
            + "używa, albo zrobi obie rzeczy naraz.",
        SettingsDetectedDevices = "Wykryte urządzenia",
        SettingsNoJoysticks = "Nie wykryto joysticków — tylko klawiatura.",
        SettingsSendingTitle = "Wysyłanie",
        SettingsHandsFree = "Hands-free — wysyłaj bez sprawdzania",
        SettingsHandsFreeOn = "To, co zostanie rozpoznane, trafia prosto na czat, razem z "
            + "przesłyszeniami i bez możliwości przeczytania czy odrzucenia. Nagrania są też "
            + "krótsze niż zwykle, bo nikt nie patrzy na to, co wyśle limit czasu.",
        SettingsHandsFreeOff = "Każda wiadomość najpierw czeka na pasku: Enter wysyła, Escape "
            + "odrzuca. W VR tego paska nie widać i nie da się na niego odpowiedzieć — po to "
            + "właśnie jest hands-free.",
        SettingsAudibleCues = "Odtwarzaj dźwięk przy starcie nagrania, wysłaniu i odrzuceniu",
        SettingsOk = "OK",
        SettingsCancel = "Anuluj",

        AboutTitle = n => $"O programie {n}",
        AboutByAuthor = a => $"autor: {a}",
        AboutReportLink = "Zgłoś błąd lub zaproponuj zmianę",
        AboutDataFolder = "Ustawienia i modele mowy:",
        AboutClose = "Zamknij",

        SendSent = "Wiadomość wysłana.",
        SendCondorNotRunning = "Condor nie jest uruchomiony.",
        SendCondorNotFocused = "Condor nie jest aktywnym oknem.",
        SendRateLimited = "Wolniej — limit częstotliwości wiadomości.",
        SendNothingToSend = "Nie ma czego wysłać.",
        SendBusy = "Poprzednia wiadomość jest jeszcze wysyłana.",
        SendUnsupportedCharacters = c => $"Nie da się tego wpisać na tym układzie klawiatury: {c}",
        SendInjectionBlocked = "Windows zablokował naciśnięcia klawiszy. Condor prawdopodobnie "
            + "działa jako administrator — uruchom Gaggle też jako administrator.",
        SendUnknown = "Nieznany wynik.",
    };

    public static UiText German { get; } = new()
    {
        MenuMicrophone = "Mikrofon",
        MenuNoMicrophones = "Keine Mikrofone gefunden",
        MenuSpeechModel = "Sprachmodell",
        MenuSpeechLanguage = "Gesprochene Sprache",
        MenuDisplayLanguage = "Anzeigesprache",
        MenuPushToTalk = "Push-to-talk…",
        MenuHandsFree = "Ohne Prüfung senden (hands-free)",
        MenuAudibleCues = "Signaltöne abspielen",
        MenuOpenConfig = "Konfigurationsdatei öffnen",
        MenuReloadConfig = "Konfiguration neu laden",
        MenuCheckForUpdates = "Nach Updates suchen…",
        MenuUpdateTo = v => $"Auf {v} aktualisieren…",
        MenuReportBug = "Fehler melden…",
        MenuSuggestIdea = "Idee vorschlagen…",
        MenuGettingStarted = "Erste Schritte…",
        MenuAbout = n => $"Über {n}…",
        MenuExit = "Beenden",

        StatusStarting = "Startet…",
        StatusHookNotInstalled = "Tastatur-Hook nicht installiert",
        StatusNeedsMultilingual = l => $"{l} braucht ein mehrsprachiges Modell",
        StatusNoModel = "kein Sprachmodell",
        StatusWaitingFor = p => $"warte auf {p}",
        StatusReady = b => $"bereit — {b} halten",

        Listening = "Höre zu…",
        Transcribing = "Erkenne…",
        Sending = "Sende…",
        MicrophoneError = m => $"Mikrofonfehler: {m}",
        NoModelLoadedSeeMenu = "Kein Sprachmodell geladen — siehe Menü im Infobereich.",
        NotRunning = p => $"{p} läuft nicht.",
        MicrophoneUnavailable = m => $"Mikrofon nicht verfügbar: {m}",
        NothingRecorded = "Nichts aufgenommen.",
        TooQuiet = "Zu leise — nichts gesendet.",
        NoModelLoaded = "Kein Sprachmodell geladen.",
        DidNotCatchThat = "Das habe ich nicht verstanden.",
        TranscriptionFailed = m => $"Spracherkennung fehlgeschlagen: {m}",

        DownloadAlreadyRunning = "Es läuft bereits ein Download.",
        SwitchedTo = n => $"Auf {n} umgestellt.",
        SwitchedToFor = (n, l) => $"Für {l} auf {n} umgestellt.",
        EnglishOnlySoLanguageIsEnglish = n => $"{n} kann nur Englisch — Sprache auf Englisch gesetzt.",
        ModelInstalled = n => $"{n} installiert.",
        Downloading = (n, p) => $"Lade {n} — {p}",
        DownloadFromHuggingFace = (n, notes) => $"{n} ({notes}) von Hugging Face herunterladen?",
        DownloadFailed = m => $"Download fehlgeschlagen: {m}",
        DownloadNow = (n, notes) => $"{n} ({notes}) jetzt herunterladen?",
        EnglishModelIsBetterAtEnglish = "Für Englisch wird ein eigenes englisches Sprachmodell "
            + "verwendet: kleiner und bei Englisch schneller als das mehrsprachige.",
        LanguageNeedsMultilingual = l => $"{l} braucht ein mehrsprachiges Sprachmodell. Die rein "
            + "englischen Modelle können das nicht — Whisper ignoriert die Anfrage und schreibt "
            + "das Gehörte als Englisch auf.",
        NoModelInstalledYet = "Es ist noch kein Sprachmodell installiert. Wähle eines im Menü "
            + "unter „Sprachmodell“.",
        NeedsMultilingualPickOne = l => $"{l} braucht ein mehrsprachiges Modell. Wähle Small oder "
            + "Medium (multilingual) unter „Sprachmodell“.",
        CouldNotLoadModel = m => $"Sprachmodell konnte nicht geladen werden: {m}",

        FinishMessageBeforeUpdating = "Beende die aktuelle Nachricht vor dem Update.",
        RestartingToFinish = "Neustart, um das Update abzuschließen…",
        CouldNotReachGitHub = "GitHub war für die Update-Prüfung nicht erreichbar.",
        IsTheLatestVersion = (n, v) => $"{n} {v} ist die neueste Version.",
        UpdateIsAvailable = (n, v) => $"{n} {v} ist verfügbar. Öffne das Menü im Infobereich, um es zu installieren.",
        UpdateFailed = m => $"Update fehlgeschlagen: {m}",
        UpdateWindowTitle = n => $"{n} aktualisieren",
        UpdateHeading = nv => $"{nv} ist verfügbar",
        UpdateWillRestart = v => $"Du hast Version {v}. Gaggle startet neu, um fertig zu werden.",
        UpdateCannotSelfInstall = v => $"Du hast Version {v}. Diese Kopie kann sich nicht selbst aktualisieren.",
        UpdateViewOnGitHub = "Dieses Release auf GitHub ansehen",
        UpdateSkipVersion = "Diese Version überspringen",
        UpdateInstallNow = "Jetzt installieren",
        UpdateOpenDownloads = "Downloads öffnen",
        UpdateLater = "Später",
        UpdateNoNotes = "Es wurden keine Release-Notes veröffentlicht.",

        AlreadyRunning = "Gaggle läuft bereits — schau im Infobereich nach.",
        HookFailedSuffix = "Push-to-talk wird nicht funktionieren. Gaggle läuft weiter, damit du "
            + "die Einstellungen prüfen kannst.",
        HandsFreeConfirm = "Hands-free sendet jede Transkription direkt in den Chat, samt "
            + "Hörfehlern und ohne die Möglichkeit, sie vorher zu lesen."
            + "\n\nGedacht ist das für VR, wo der Streifen nicht zu sehen und nicht zu "
            + "beantworten ist. Am Monitor gibst du damit die einzige menschliche Kontrolle "
            + "darüber auf, was die anderen Piloten bekommen."
            + "\n\nEinschalten?",

        SettingsTitle = "Gaggle-Einstellungen",
        SettingsPushToTalkTitle = "Push-to-talk",
        SettingsChange = "Ändern…",
        SettingsStop = "Stopp",
        SettingsPressAKey = "Taste oder Knopf drücken…",
        SettingsCapturing = "Nimmt auf. Jeder Tastendruck wird abgefangen — wenn du es dir anders "
            + "überlegst, nimm die Maus und den Stopp-Knopf.",
        SettingsKeyHidden = "Diese Taste wird vor Condor verborgen, solange Gaggle läuft.",
        SettingsButtonNotHidden = "Joystick-Tasten lassen sich vor Condor nicht verbergen.",
        SettingsJoystickWarning = "Condor sieht diese Taste trotzdem. Nimm eine, die der Simulator nicht "
            + "benutzt, sonst passiert beides gleichzeitig.",
        SettingsDetectedDevices = "Erkannte Geräte",
        SettingsNoJoysticks = "Keine Joysticks erkannt — nur Tastatur.",
        SettingsSendingTitle = "Senden",
        SettingsHandsFree = "Hands-free — ohne Prüfung senden",
        SettingsHandsFreeOn = "Was erkannt wurde, geht direkt in den Chat, samt Hörfehlern und "
            + "ohne dass sich etwas lesen oder verwerfen ließe. Aufnahmen werden außerdem kürzer "
            + "abgeschnitten, weil niemand mitliest, was das Zeitlimit sendet.",
        SettingsHandsFreeOff = "Jede Nachricht wartet zuerst im Streifen: Enter sendet, Escape "
            + "verwirft. In VR ist dieser Streifen nicht zu sehen und nicht zu beantworten — "
            + "genau dafür gibt es hands-free.",
        SettingsAudibleCues = "Ton abspielen bei Aufnahmestart, Senden und Verwerfen",
        SettingsOk = "OK",
        SettingsCancel = "Abbrechen",

        AboutTitle = n => $"Über {n}",
        AboutByAuthor = a => $"von {a}",
        AboutReportLink = "Fehler melden oder Idee vorschlagen",
        AboutDataFolder = "Einstellungen und Sprachmodelle:",
        AboutClose = "Schließen",

        SendSent = "Nachricht gesendet.",
        SendCondorNotRunning = "Condor läuft nicht.",
        SendCondorNotFocused = "Condor ist nicht das aktive Fenster.",
        SendRateLimited = "Langsamer — Sendelimit aktiv.",
        SendNothingToSend = "Nichts zu senden.",
        SendBusy = "Die vorherige Nachricht wird noch gesendet.",
        SendUnsupportedCharacters = c => $"Auf diesem Tastaturlayout nicht tippbar: {c}",
        SendInjectionBlocked = "Windows hat die Tastendrücke blockiert. Condor läuft vermutlich "
            + "als Administrator — starte Gaggle ebenfalls als Administrator.",
        SendUnknown = "Unbekanntes Ergebnis.",
    };

    public static UiText Spanish { get; } = new()
    {
        MenuMicrophone = "Micrófono",
        MenuNoMicrophones = "No se han encontrado micrófonos",
        MenuSpeechModel = "Modelo de voz",
        MenuSpeechLanguage = "Idioma hablado",
        MenuDisplayLanguage = "Idioma de la interfaz",
        MenuPushToTalk = "Push-to-talk…",
        MenuHandsFree = "Enviar sin revisar (hands-free)",
        MenuAudibleCues = "Reproducir tonos",
        MenuOpenConfig = "Abrir el archivo de configuración",
        MenuReloadConfig = "Recargar la configuración",
        MenuCheckForUpdates = "Buscar actualizaciones…",
        MenuUpdateTo = v => $"Actualizar a {v}…",
        MenuReportBug = "Informar de un error…",
        MenuSuggestIdea = "Proponer una idea…",
        MenuGettingStarted = "Primeros pasos…",
        MenuAbout = n => $"Acerca de {n}…",
        MenuExit = "Salir",

        StatusStarting = "Iniciando…",
        StatusHookNotInstalled = "hook de teclado no instalado",
        StatusNeedsMultilingual = l => $"{l} necesita un modelo multilingüe",
        StatusNoModel = "sin modelo de voz",
        StatusWaitingFor = p => $"esperando a {p}",
        StatusReady = b => $"listo — mantén {b}",

        Listening = "Escuchando…",
        Transcribing = "Transcribiendo…",
        Sending = "Enviando…",
        MicrophoneError = m => $"Error del micrófono: {m}",
        NoModelLoadedSeeMenu = "No hay modelo de voz cargado — mira el menú del icono.",
        NotRunning = p => $"{p} no se está ejecutando.",
        MicrophoneUnavailable = m => $"Micrófono no disponible: {m}",
        NothingRecorded = "No se ha grabado nada.",
        TooQuiet = "Demasiado bajo — no se ha enviado nada.",
        NoModelLoaded = "No hay modelo de voz cargado.",
        DidNotCatchThat = "No lo he entendido.",
        TranscriptionFailed = m => $"La transcripción ha fallado: {m}",

        DownloadAlreadyRunning = "Ya hay una descarga en curso.",
        SwitchedTo = n => $"Cambiado a {n}.",
        SwitchedToFor = (n, l) => $"Cambiado a {n} para {l}.",
        EnglishOnlySoLanguageIsEnglish = n => $"{n} es solo para inglés — idioma cambiado a inglés.",
        ModelInstalled = n => $"{n} instalado.",
        Downloading = (n, p) => $"Descargando {n} — {p}",
        DownloadFromHuggingFace = (n, notes) => $"¿Descargar {n} ({notes}) de Hugging Face?",
        DownloadFailed = m => $"La descarga ha fallado: {m}",
        DownloadNow = (n, notes) => $"¿Descargar {n} ({notes}) ahora?",
        EnglishModelIsBetterAtEnglish = "El inglés usa un modelo de voz dedicado, más pequeño y "
            + "más rápido con el inglés que el multilingüe.",
        LanguageNeedsMultilingual = l => $"{l} necesita un modelo de voz multilingüe. Los modelos "
            + "solo en inglés no pueden transcribirlo: Whisper ignora la petición y escribe lo que "
            + "ha oído como si fuera inglés.",
        NoModelInstalledYet = "Todavía no hay ningún modelo de voz instalado. Elige uno en el menú "
            + "del icono, en «Modelo de voz».",
        NeedsMultilingualPickOne = l => $"{l} necesita un modelo multilingüe. Elige Small o Medium "
            + "(multilingual) en «Modelo de voz».",
        CouldNotLoadModel = m => $"No se ha podido cargar el modelo de voz: {m}",

        FinishMessageBeforeUpdating = "Termina el mensaje actual antes de actualizar.",
        RestartingToFinish = "Reiniciando para terminar la actualización…",
        CouldNotReachGitHub = "No se ha podido conectar con GitHub para buscar actualizaciones.",
        IsTheLatestVersion = (n, v) => $"{n} {v} es la última versión.",
        UpdateIsAvailable = (n, v) => $"{n} {v} está disponible. Abre el menú del icono para instalarlo.",
        UpdateFailed = m => $"La actualización ha fallado: {m}",
        UpdateWindowTitle = n => $"Actualizar {n}",
        UpdateHeading = nv => $"{nv} está disponible",
        UpdateWillRestart = v => $"Tienes la versión {v}. Gaggle se reiniciará para terminar.",
        UpdateCannotSelfInstall = v => $"Tienes la versión {v}. Esta copia no puede actualizarse sola.",
        UpdateViewOnGitHub = "Ver esta versión en GitHub",
        UpdateSkipVersion = "Omitir esta versión",
        UpdateInstallNow = "Instalar ahora",
        UpdateOpenDownloads = "Abrir descargas",
        UpdateLater = "Más tarde",
        UpdateNoNotes = "No se han publicado notas de la versión.",

        AlreadyRunning = "Gaggle ya se está ejecutando — búscalo en el área de notificación.",
        HookFailedSuffix = "El push-to-talk no funcionará. Gaggle seguirá abierto para que puedas "
            + "revisar los ajustes.",
        HandsFreeConfirm = "El modo hands-free envía cada transcripción directamente al chat, "
            + "con los errores de audición incluidos y sin poder leerla antes."
            + "\n\nExiste para VR, donde la franja de revisión no se ve ni se puede responder. En "
            + "un monitor estás renunciando al único control humano sobre lo que reciben los "
            + "demás pilotos."
            + "\n\n¿Activarlo?",

        SettingsTitle = "Ajustes de Gaggle",
        SettingsPushToTalkTitle = "Push-to-talk",
        SettingsChange = "Cambiar…",
        SettingsStop = "Parar",
        SettingsPressAKey = "Pulsa una tecla o un botón…",
        SettingsCapturing = "Escuchando. Se captura cada pulsación, así que si cambias de idea usa "
            + "el ratón y el botón Parar.",
        SettingsKeyHidden = "Esta tecla se oculta a Condor mientras Gaggle está en marcha.",
        SettingsButtonNotHidden = "Los botones del joystick no se pueden ocultar a Condor.",
        SettingsJoystickWarning = "Condor seguirá viendo este botón. Elige uno que el simulador no use, o "
            + "hará las dos cosas a la vez.",
        SettingsDetectedDevices = "Dispositivos detectados",
        SettingsNoJoysticks = "No se han detectado joysticks — solo teclado.",
        SettingsSendingTitle = "Envío",
        SettingsHandsFree = "Hands-free — enviar sin revisar",
        SettingsHandsFreeOn = "Lo que se transcriba va directo al chat, con los errores de "
            + "audición incluidos y sin nada que leer ni descartar. Además las grabaciones se "
            + "cortan antes, porque nadie está mirando lo que envía el límite de tiempo.",
        SettingsHandsFreeOff = "Cada mensaje espera primero en la franja: Enter lo envía, Escape "
            + "lo descarta. En VR esa franja no se ve ni se puede responder — para eso está el "
            + "modo hands-free.",
        SettingsAudibleCues = "Reproducir un tono al empezar a grabar, al enviar y al descartar",
        SettingsOk = "Aceptar",
        SettingsCancel = "Cancelar",

        AboutTitle = n => $"Acerca de {n}",
        AboutByAuthor = a => $"por {a}",
        AboutReportLink = "Informar de un error o proponer una idea",
        AboutDataFolder = "Ajustes y modelos de voz:",
        AboutClose = "Cerrar",

        SendSent = "Mensaje enviado.",
        SendCondorNotRunning = "Condor no se está ejecutando.",
        SendCondorNotFocused = "Condor no es la ventana activa.",
        SendRateLimited = "Más despacio — límite de frecuencia activo.",
        SendNothingToSend = "No hay nada que enviar.",
        SendBusy = "Todavía se está enviando el mensaje anterior.",
        SendUnsupportedCharacters = c => $"No se puede escribir con esta distribución de teclado: {c}",
        SendInjectionBlocked = "Windows ha bloqueado las pulsaciones. Probablemente Condor se está "
            + "ejecutando como administrador — ejecuta Gaggle como administrador también.",
        SendUnknown = "Resultado desconocido.",
    };
}
