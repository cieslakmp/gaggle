using Gaggle.Speech;

namespace Gaggle.Localisation;

/// <summary>
/// What happens to one utterance: the overlay status line from key-down to sent, the
/// send outcomes, and the errors that surface from the speech, download and update
/// paths.
///
/// The model and language lookups live here too. Both are half identifier and half
/// prose: "Base" and "Small" are the names whisper.cpp publishes and stay put, while
/// everything around them is a sentence.
/// </summary>
public sealed partial class Strings
{
    // ------------------------------------------------------- Utterance statuses

    public string NoModelLoadedSeeMenu =>
        Pick(
            "No speech model loaded — see the tray menu.",
            "Nie wczytano modelu mowy — sprawdź menu zasobnika.");

    public string ProcessNotRunning(string processName) =>
        Pick($"{processName} is not running.", $"{processName} nie jest uruchomiony.");

    public string MicrophoneUnavailable(string detail) =>
        Pick($"Microphone unavailable: {detail}", $"Mikrofon niedostępny: {detail}");

    public string MicrophoneError(string detail) =>
        Pick($"Microphone error: {detail}", $"Błąd mikrofonu: {detail}");

    public string Listening => Pick("Listening…", "Słucham…");

    public string NothingRecorded => Pick("Nothing recorded.", "Nic nie nagrano.");

    public string Transcribing => Pick("Transcribing…", "Rozpoznawanie…");

    public string TooQuiet => Pick("Too quiet — nothing sent.", "Za cicho — nic nie wysłano.");

    public string HeldTooLong =>
        Pick("Held past the limit — nothing sent.", "Przytrzymane za długo — nic nie wysłano.");

    public string NoModelLoaded => Pick("No speech model loaded.", "Nie wczytano modelu mowy.");

    public string DidNotCatchThat => Pick("Did not catch that.", "Nie zrozumiałem.");

    public string TranscriptionFailed(string detail) =>
        Pick($"Transcription failed: {detail}", $"Rozpoznawanie nie powiodło się: {detail}");

    public string Sending => Pick("Sending…", "Wysyłanie…");

    // ------------------------------------------------------------ Send outcomes

    public string SendSent => Pick("Message sent.", "Wiadomość wysłana.");

    public string SendCondorNotRunning =>
        Pick("Condor is not running.", "Condor nie jest uruchomiony.");

    public string SendCondorNotFocused =>
        Pick("Condor is not the active window.", "Condor nie jest aktywnym oknem.");

    public string SendRateLimited =>
        Pick("Slow down — rate limit active.", "Wolniej — limit częstotliwości.");

    public string SendNothingToSend => Pick("Nothing to send.", "Nie ma czego wysłać.");

    public string SendBusy =>
        Pick("Still sending the previous message.", "Trwa wysyłanie poprzedniej wiadomości.");

    public string SendUnsupportedCharacters(string? detail) =>
        Pick(
            $"Cannot type on this keyboard layout: {detail}",
            $"Nie da się wpisać przy tym układzie klawiatury: {detail}");

    public string SendInjectionBlocked =>
        Pick(
            "Windows blocked the keystrokes. Condor is probably running as "
                + "administrator — run Gaggle as administrator too.",
            "Windows zablokował wciśnięcia klawiszy. Condor prawdopodobnie działa jako "
                + "administrator — uruchom Gaggle również jako administrator.");

    public string SendUnknown => Pick("Unknown result.", "Nieznany wynik.");

    // ----------------------------------------------------- Downloads and models

    /// <summary>The only translated word is "of"; the numbers are formatted by the caller.</summary>
    public string DownloadedOf(string percent, string received, string total) =>
        Pick($"{percent} — {received} of {total} MB", $"{percent} — {received} z {total} MB");

    public string DownloadingModel(string name, string progress) =>
        Pick($"Downloading {name} — {progress}", $"Pobieranie {name} — {progress}");

    public string DownloadingUpdate(string version, string progress) =>
        Pick($"Downloading {version} — {progress}", $"Pobieranie {version} — {progress}");

    public string ModelDownloadInProgress =>
        Pick("A model download is already running.", "Pobieranie modelu już trwa.");

    public string DownloadInProgress =>
        Pick("A download is already running.", "Pobieranie już trwa.");

    public string SwitchedToModel(string name) =>
        Pick($"Switched to {name}.", $"Przełączono na {name}.");

    public string SwitchedToModelFor(string name, string language) =>
        Pick($"Switched to {name} for {language}.", $"Przełączono na {name} dla: {language}.");

    public string ModelIsEnglishOnly(string name) =>
        Pick(
            $"{name} is English only — language set to English.",
            $"{name} obsługuje tylko angielski — ustawiono język angielski.");

    public string DownloadTooLarge =>
        Pick(
            "That download is larger than it should be, so it was stopped.",
            "To pobieranie jest większe, niż powinno, więc zostało przerwane.");

    public string ModelChecksumMismatch(string name) =>
        Pick(
            $"{name} did not match its published checksum, so it was discarded.",
            $"{name} nie zgadza się z opublikowaną sumą kontrolną, więc został odrzucony.");

    public string ModelInstalled(string name) =>
        Pick($"{name} installed.", $"Zainstalowano {name}.");

    public string ModelFileNotFound(string path) =>
        Pick(
            $"Whisper model not found at {path}. Download one from the tray menu.",
            $"Nie znaleziono modelu Whisper w {path}. Pobierz go z menu zasobnika.");

    /// <summary>
    /// The name of a ggml build. The tier — Tiny, Base, Small, Medium — is what
    /// whisper.cpp calls it and is not translated; only the parenthesis is.
    /// </summary>
    public string ModelName(string fileName) => fileName switch
    {
        "ggml-tiny.en.bin" => Pick("Tiny (English)", "Tiny (angielski)"),
        "ggml-base.en.bin" => Pick("Base (English)", "Base (angielski)"),
        "ggml-small.en.bin" => Pick("Small (English)", "Small (angielski)"),
        "ggml-small.bin" => Pick("Small (multilingual)", "Small (wielojęzyczny)"),
        "ggml-medium.bin" => Pick("Medium (multilingual)", "Medium (wielojęzyczny)"),
        _ => Fallback(fileName)?.Name ?? fileName,
    };

    /// <summary>The size-and-suitability note shown beside a model in the menu.</summary>
    public string ModelNotes(string fileName) => fileName switch
    {
        "ggml-tiny.en.bin" => Pick(
            "~75 MB, fastest, noticeably weaker on jargon",
            "~75 MB, najszybszy, wyraźnie słabszy przy żargonie"),
        "ggml-base.en.bin" => Pick(
            "~148 MB, good default for short radio calls",
            "~148 MB, dobry domyślny wybór do krótkich zgłoszeń"),
        "ggml-small.en.bin" => Pick(
            "~488 MB, best accuracy, ~2x slower",
            "~488 MB, najlepsza dokładność, ~2x wolniejszy"),
        "ggml-small.bin" => Pick(
            "~488 MB, needed for Polish/German/Spanish",
            "~488 MB, potrzebny do polskiego/niemieckiego/hiszpańskiego"),
        "ggml-medium.bin" => Pick(
            "~1.5 GB, best non-English accuracy, ~3x slower",
            "~1,5 GB, najlepszy poza angielskim, ~3x wolniejszy"),
        // Only reachable from a hand-edited config: every menu caption comes from
        // ModelInstaller.Available, and every file in it is named above.
        _ => Fallback(fileName)?.Notes
            ?? Pick("not one of the models Gaggle offers", "model spoza listy Gaggle"),
    };

    /// <summary>
    /// The spoken language, as the menu names it. The per-language endonyms are the same
    /// text whatever the interface language is — "Polski" is "Polski" — so only the two
    /// menu rows have anything to translate.
    /// </summary>
    public string SpokenLanguageName(string code) => code switch
    {
        SpokenLanguage.EnglishCode => Pick("English", "Angielski"),
        SpokenLanguage.AutoCode => Pick("Any language → English", "Dowolny język → angielski"),
        _ => SpokenLanguage.Describe(code),
    };

    // -------------------------------------------------------- Input and updates

    public string Unbound => Pick("Unbound", "Nieprzypisany");

    public string StickButton(int stick, int button) =>
        Pick($"Stick {stick} · button {button}", $"Joystick {stick} · przycisk {button}");

    public string KeyboardHookFailed(int error) =>
        Pick(
            $"Could not install the keyboard hook (Win32 error {error}).",
            $"Nie udało się zainstalować haka klawiatury (błąd Win32 {error}).");

    public string NotANormalInstall =>
        Pick(
            "This copy of Gaggle is not a normal install, so it cannot update itself.",
            "Ta kopia Gaggle nie jest zwykłą instalacją, więc nie może się zaktualizować.");

    public string NoVerifiablePackage =>
        Pick(
            "That release does not publish a verifiable Windows package.",
            "To wydanie nie udostępnia weryfikowalnej paczki dla Windows.");

    public string CouldNotFetchChecksum =>
        Pick(
            "Could not fetch the checksum for the update.",
            "Nie udało się pobrać sumy kontrolnej aktualizacji.");

    public string ChecksumUnreadable =>
        Pick(
            "The published checksum could not be read.",
            "Nie udało się odczytać opublikowanej sumy kontrolnej.");

    public string ChecksumMismatch =>
        Pick(
            "The download did not match its published checksum, so it was discarded.",
            "Pobrany plik nie zgadza się z opublikowaną sumą kontrolną, więc został odrzucony.");

    public string PackageMissingFiles =>
        Pick(
            "The update package is missing files Gaggle needs, so it was not installed.",
            "W paczce aktualizacji brakuje plików potrzebnych Gaggle, więc jej nie zainstalowano.");

    public string CouldNotStartUpdater =>
        Pick("Could not start the updater.", "Nie udało się uruchomić aktualizatora.");

    public string FinishMessageBeforeUpdating =>
        Pick(
            "Finish the current message before updating.",
            "Dokończ bieżącą wiadomość przed aktualizacją.");

    public string RestartingToFinishUpdate =>
        Pick(
            "Restarting to finish the update…",
            "Ponowne uruchamianie, aby dokończyć aktualizację…");

    public string AlreadyRunning =>
        Pick(
            "Gaggle is already running — look for it in the notification area.",
            "Gaggle jest już uruchomiony — poszukaj go w obszarze powiadomień.");

    private static ModelInstaller.ModelChoice? Fallback(string fileName) =>
        ModelInstaller.Available.FirstOrDefault(choice => choice.FileName == fileName);
}
