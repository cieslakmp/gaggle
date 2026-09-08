namespace Gaggle.Localisation;

/// <summary>
/// The tray icon: menu captions, the status line, the balloon tips and the questions
/// asked from the menu.
///
/// The status line doubles as the NotifyIcon tooltip, which throws above 63 characters
/// and is truncated to fit. The Polish wording of the Status* members is kept short on
/// purpose, and StringsTests holds the budget.
/// </summary>
public sealed partial class Strings
{
    // ------------------------------------------------------------- Menu captions

    public string Starting => Pick("Starting…", "Uruchamianie…");

    public string Microphone => Pick("Microphone", "Mikrofon");

    public string SpeechModel => Pick("Speech model", "Model mowy");

    /// <summary>
    /// The language the pilot speaks. Named apart from <see cref="AppLanguage"/> because
    /// the two sit next to each other in the menu and mean entirely different things.
    /// </summary>
    public string SpeechLanguage => Pick("Speech language", "Język mowy");

    public string AppLanguage => Pick("App language", "Język aplikacji");

    public string PushToTalkSettings => Pick("Push-to-talk…", "Przycisk nadawania…");

    public string HandsFreeItem =>
        Pick("Send without review (hands-free)", "Wysyłaj bez podglądu (bez rąk)");

    public string AudibleCuesItem => Pick("Play audible cues", "Odtwarzaj sygnały dźwiękowe");

    public string OpenConfigFile => Pick("Open config file", "Otwórz plik konfiguracyjny");

    public string ReloadConfigItem => Pick("Reload config", "Wczytaj konfigurację ponownie");

    public string CheckForUpdates => Pick("Check for updates…", "Sprawdź aktualizacje…");

    public string UpdateTo(string version) =>
        Pick($"Update to {version}…", $"Zaktualizuj do {version}…");

    public string ReportABug => Pick("Report a bug…", "Zgłoś błąd…");

    public string SuggestAnIdea => Pick("Suggest an idea…", "Zaproponuj zmianę…");

    public string AboutItem(string appName) =>
        Pick($"About {appName}…", $"O programie {appName}…");

    public string Exit => Pick("Exit", "Zakończ");

    public string NoMicrophonesFound =>
        Pick("No microphones found", "Nie znaleziono mikrofonów");

    // -------------------------------------------------------------- Status line

    public string StatusHookNotInstalled =>
        Pick("keyboard hook not installed", "hak klawiatury nie działa");

    public string StatusNeedsMultilingualModel(string language) =>
        Pick(
            $"{language} needs a multilingual model",
            $"{language} wymaga modelu wielojęzycznego");

    public string StatusNoSpeechModel => Pick("no speech model", "brak modelu mowy");

    public string StatusWaitingFor(string processName) =>
        Pick($"waiting for {processName}", $"czekam na {processName}");

    public string StatusReady(string binding) =>
        Pick($"ready — hold {binding}", $"gotowe — trzymaj {binding}");

    // ----------------------------------------------------------- Balloon tips

    public string NoModelInstalledBalloon =>
        Pick(
            "No speech model installed yet. Pick one from the tray menu under Speech model.",
            "Nie zainstalowano jeszcze modelu mowy. Wybierz go w menu zasobnika, "
                + "w sekcji Model mowy.");

    public string NeedsMultilingualModelBalloon(string language) =>
        Pick(
            $"{language} needs a multilingual model. "
                + "Pick Small or Medium (multilingual) under Speech model.",
            $"{language} wymaga modelu wielojęzycznego. "
                + "Wybierz Small lub Medium (wielojęzyczny) w sekcji Model mowy.");

    public string CouldNotLoadModel(string detail) =>
        Pick(
            $"Could not load the speech model: {detail}",
            $"Nie udało się wczytać modelu mowy: {detail}");

    public string UpdateAvailableBalloon(string appName, string version) =>
        Pick(
            $"{appName} {version} is available. Open the tray menu to install it.",
            $"Dostępna jest wersja {appName} {version}. Otwórz menu zasobnika, aby ją "
                + "zainstalować.");

    // ------------------------------------------------------------ Message boxes

    public string PushToTalkUnavailable(string detail) =>
        Pick(
            detail + "\n\nPush-to-talk will not work. Gaggle will keep running so you "
                + "can check settings.",
            detail + "\n\nPrzycisk nadawania nie będzie działać. Gaggle pozostanie "
                + "uruchomiony, żebyś mógł sprawdzić ustawienia.");

    public string EnglishModelOffer(string name, string notes) =>
        Pick(
            "English uses a dedicated English speech model, which is smaller and "
                + "faster at English than the multilingual one."
                + "\n\n"
                + $"Download {name} ({notes}) now?",
            "Angielski korzysta z osobnego modelu angielskiego, który jest mniejszy i "
                + "szybszy w angielskim niż model wielojęzyczny."
                + "\n\n"
                + $"Pobrać teraz {name} ({notes})?");

    public string MultilingualModelOffer(string language, string name, string notes) =>
        Pick(
            $"{language} needs a multilingual speech model. The English-only models "
                + "cannot transcribe it — Whisper ignores the request and writes down "
                + "what it heard as English instead."
                + "\n\n"
                + $"Download {name} ({notes}) now?",
            $"{language} wymaga wielojęzycznego modelu mowy. Modele wyłącznie "
                + "angielskie tego nie potrafią — Whisper zignoruje prośbę i zapisze "
                + "usłyszany dźwięk tak, jakby był po angielsku."
                + "\n\n"
                + $"Pobrać teraz {name} ({notes})?");

    public string DownloadModelQuestion(string name, string notes) =>
        Pick(
            $"Download {name} ({notes}) from Hugging Face?",
            $"Pobrać {name} ({notes}) z Hugging Face?");

    public string DownloadFailed(string detail) =>
        Pick($"Download failed: {detail}", $"Pobieranie nie powiodło się: {detail}");

    public string HandsFreeWarning =>
        Pick(
            "Hands-free sends every transcript straight to chat, mishearings included, "
                + "with no chance to read it first."
                + "\n\n"
                + "It exists for VR, where the review overlay cannot be seen or "
                + "answered. On a monitor you are giving up the only human check on "
                + "what other pilots receive."
                + "\n\n"
                + "Turn it on?",
            "Tryb bez rąk wysyła każdą transkrypcję prosto na czat, razem z "
                + "przesłyszeniami, bez możliwości przeczytania jej wcześniej."
                + "\n\n"
                + "Istnieje z myślą o VR, gdzie okna podglądu nie da się zobaczyć ani "
                + "potwierdzić. Na monitorze rezygnujesz z jedynej ludzkiej kontroli "
                + "nad tym, co dostaną inni piloci."
                + "\n\n"
                + "Włączyć go?");

    public string CouldNotReachGitHub =>
        Pick(
            "Could not reach GitHub to check for updates.",
            "Nie udało się połączyć z GitHubem, aby sprawdzić aktualizacje.");

    public string AlreadyLatest(string appName, string version) =>
        Pick(
            $"{appName} {version} is the latest version.",
            $"{appName} {version} to najnowsza wersja.");

    public string UpdateFailed(string detail) =>
        Pick($"Update failed: {detail}", $"Aktualizacja nie powiodła się: {detail}");
}
