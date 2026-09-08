namespace Gaggle.Localisation;

/// <summary>
/// The four windows: settings, about, update, and the review overlay's hint line.
///
/// These are the strings that decide how wide a dialog has to be. All three dialogs
/// size themselves to their content, so a longer translation grows the window rather
/// than being cut off — see the layout comment in <see cref="Ui.SettingsForm"/>.
/// </summary>
public sealed partial class Strings
{
    // ---------------------------------------------------------------- Shared

    /// <summary>The same word in both, and still a translation rather than a constant.</summary>
    public string Ok => Pick("OK", "OK");

    public string Cancel => Pick("Cancel", "Anuluj");

    public string Close => Pick("Close", "Zamknij");

    // -------------------------------------------------------------- Settings

    public string SettingsTitle => Pick("Gaggle settings", "Ustawienia Gaggle");

    public string PushToTalkHeading => Pick("Push-to-talk", "Przycisk nadawania");

    public string ChangeBinding => Pick("Change…", "Zmień…");

    public string StopCapture => Pick("Stop", "Zatrzymaj");

    public string PressAKeyOrButton =>
        Pick("Press a key or button…", "Naciśnij klawisz lub przycisk…");

    public string CaptureListening =>
        Pick(
            "Listening. Every keystroke is captured, so use the Stop button with the "
                + "mouse if you change your mind.",
            "Nasłuchiwanie. Przechwytywany jest każdy klawisz, więc jeśli zmienisz "
                + "zdanie, użyj myszą przycisku Zatrzymaj.");

    public string KeyHiddenFromCondor =>
        Pick(
            "This key is hidden from Condor while Gaggle is running.",
            "Ten klawisz jest ukryty przed Condorem, dopóki Gaggle działa.");

    public string ButtonsCannotBeHidden =>
        Pick(
            "Joystick buttons cannot be hidden from Condor.",
            "Przycisków joysticka nie da się ukryć przed Condorem.");

    public string JoystickWarning =>
        Pick(
            "Condor will still see this button. Pick one the sim does not use, or it "
                + "will do both things at once.",
            "Condor i tak zobaczy ten przycisk. Wybierz taki, którego symulator nie "
                + "używa, bo inaczej zrobi obie rzeczy naraz.");

    public string DetectedDevices => Pick("Detected devices", "Wykryte urządzenia");

    public string NoJoysticksDetected =>
        Pick(
            "No joysticks detected — keyboard only.",
            "Nie wykryto joysticków — tylko klawiatura.");

    public string JoystickDevice(int stick, string name, int buttons) =>
        Pick(
            $"Stick {stick}: {name} ({buttons} buttons)",
            $"Joystick {stick}: {name} ({buttons} przycisków)");

    public string SendingHeading => Pick("Sending", "Wysyłanie");

    public string HandsFreeCheckbox =>
        Pick(
            "Hands-free — send without reviewing",
            "Tryb automatyczny — wysyłaj bez podglądu");

    public string HandsFreeOnNote =>
        Pick(
            "Whatever is transcribed goes straight into chat, mishearings included, "
                + "with nothing to read or discard first. Keep holding past the "
                + "recording limit and the recording is thrown away instead — with no "
                + "overlay to answer, that is the only way to take a message back.",
            "Cokolwiek zostanie rozpoznane, trafia prosto na czat, razem z "
                + "przesłyszeniami, bez możliwości przeczytania lub odrzucenia. "
                + "Przytrzymaj dłużej niż limit nagrania, a nagranie zostanie "
                + "skasowane — bez okna podglądu to jedyny sposób, żeby wycofać "
                + "wiadomość.");

    public string HandsFreeOffNote =>
        Pick(
            "Every message waits in the overlay first: Enter sends it, Escape discards "
                + "it, and holding past the recording limit throws it away before it "
                + "gets there. In VR that overlay cannot be seen or answered — that is "
                + "what hands-free is for.",
            "Każda wiadomość czeka najpierw w oknie podglądu: Enter wysyła, Escape "
                + "odrzuca, a przytrzymanie dłużej niż limit nagrania kasuje ją, zanim "
                + "tam trafi. W VR tego okna nie da się zobaczyć ani potwierdzić — i po "
                + "to jest tryb automatyczny.");

    public string AudibleCuesCheckbox =>
        Pick(
            "Play a tone when recording starts, sends, or is dropped",
            "Odtwórz dźwięk przy starcie nagrania, wysłaniu i odrzuceniu");

    // ----------------------------------------------------------------- About

    public string AboutTitle(string appName) =>
        Pick($"About {appName}", $"O programie {appName}");

    public string ByAuthor(string author) => Pick($"by {author}", $"autor: {author}");

    public string ReportBugOrIdea =>
        Pick(
            "Report a bug or suggest an idea",
            "Zgłoś błąd lub zaproponuj zmianę");

    public string SettingsAndModels =>
        Pick("Settings and speech models:", "Ustawienia i modele mowy:");

    // ---------------------------------------------------------------- Update

    public string UpdateTitle(string appName) =>
        Pick($"Update {appName}", $"Aktualizacja {appName}");

    public string VersionIsAvailable(string appName, string version) =>
        Pick(
            $"{appName} {version} is available",
            $"Dostępna jest wersja {appName} {version}");

    public string RunningWillRestart(string appName, string version) =>
        Pick(
            $"You are running {version}. {appName} will restart to finish.",
            $"Masz wersję {version}. {appName} uruchomi się ponownie, aby zakończyć.");

    public string RunningCannotUpdate(string version) =>
        Pick(
            $"You are running {version}. This copy cannot update itself.",
            $"Masz wersję {version}. Ta kopia nie może się sama zaktualizować.");

    public string ViewReleaseOnGitHub =>
        Pick("View this release on GitHub", "Zobacz to wydanie na GitHubie");

    public string SkipThisVersion => Pick("Skip this version", "Pomiń tę wersję");

    public string Later => Pick("Later", "Później");

    public string InstallNow => Pick("Install now", "Zainstaluj teraz");

    public string OpenDownloads => Pick("Open downloads", "Otwórz pobieranie");

    public string NoReleaseNotes =>
        Pick(
            "No release notes were published.",
            "Nie opublikowano opisu zmian.");

    // --------------------------------------------------------------- Overlay

    /// <summary>
    /// The line under the transcript. Keys are named by Windows and are not translated:
    /// the key is labelled "Enter" on a Polish keyboard too.
    /// </summary>
    public string ReviewHint(string confirmKey, string cancelKey) =>
        Pick(
            $"{confirmKey} to send    ·    {cancelKey} to discard",
            $"{confirmKey} — wyślij    ·    {cancelKey} — odrzuć");
}
