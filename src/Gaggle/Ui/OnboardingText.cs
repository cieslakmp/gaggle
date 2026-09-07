namespace Gaggle.Ui;

/// <summary>
/// Everything the getting-started window says, in one language.
///
/// A record with named members rather than a dictionary of keys on purpose: adding a
/// line to the guide fails the build until every language has one. A missing key would
/// otherwise show up as a blank label in somebody else's language, which is exactly the
/// kind of fault nobody who reads English would ever see.
///
/// Separate from <see cref="UiText"/>, which holds the rest of the interface: this is
/// one long block of prose that is rewritten as a whole, and mixing it into the string
/// table would bury eighty short labels under six paragraphs. Both draw on the same
/// <see cref="UiLanguages"/> list, so neither can offer a language the other lacks.
/// </summary>
public sealed record OnboardingText(
    string LanguageName,
    string WindowTitle,
    string Heading,
    string Intro,
    string Step1Title,
    string Step1Body,
    string Step2Title,
    string Step2Body,
    string Step3Title,
    string Step3Body,
    string Step4Title,
    string Step4Body,
    string EtiquetteTitle,
    string EtiquetteBody,
    string Footer,
    string LanguageLabel,
    string CloseButton)
{
    /// <summary>
    /// The languages the guide exists in, in the order the picker offers them. English
    /// leads because it is both the default and the fallback.
    /// </summary>
    public static IReadOnlyList<string> AvailableCodes => UiLanguages.Codes;

    /// <summary>
    /// The guide in the requested language, falling back to English for anything else.
    ///
    /// A hand-edited config naming a language nobody has written yet gets English rather
    /// than an empty window.
    /// </summary>
    public static OnboardingText For(string? languageCode) => UiLanguages.Normalise(languageCode) switch
    {
        "pl" => Polish,
        "de" => German,
        "es" => Spanish,
        _ => English,
    };

    public static OnboardingText English { get; } = new(
        LanguageName: "English",
        WindowTitle: "Getting started with Gaggle",
        Heading: "Welcome to Gaggle",
        Intro: "Hold a key, say something, and Gaggle types it into Condor's chat. Your "
            + "speech is transcribed on this machine — nothing is sent to a server.",
        Step1Title: "1. Choose a speech model",
        Step1Body: "Right-click the tray icon → “Speech model”. “Base” is a good start: it "
            + "downloads once and then works offline. Nothing happens until a model is installed.",
        Step2Title: "2. Choose your microphone",
        Step2Body: "Right-click the tray icon → “Microphone”, and pick the one you actually "
            + "fly with — usually the one in your headset.",
        Step3Title: "3. Set push-to-talk",
        Step3Body: "Right-click → “Push-to-talk…”, press “Change”, then press the key or "
            + "joystick button you want. Keyboard keys are hidden from Condor; joystick "
            + "buttons are not, so pick one the sim does not already use.",
        Step4Title: "4. In flight",
        Step4Body: "Hold the button, speak, release. What it heard appears in a strip near "
            + "the bottom of the screen: Enter sends it, Escape discards it. In VR you cannot "
            + "see that strip, so turn on hands-free in the tray menu and messages go straight out.",
        EtiquetteTitle: "Before you fly",
        EtiquetteBody: "This types into a chat everyone in the race can read. Transcription "
            + "is never perfect, so read what it heard before you send it — at least until "
            + "you trust it. There is a rate limit, and it is there on purpose.",
        Footer: "Everything lives in the tray icon's right-click menu: the settings, this "
            + "guide, and “Report a bug…”.",
        LanguageLabel: "Language",
        CloseButton: "Close");

    public static OnboardingText Polish { get; } = new(
        LanguageName: "Polski",
        WindowTitle: "Pierwsze kroki z Gaggle",
        Heading: "Witaj w Gaggle",
        Intro: "Przytrzymaj klawisz, powiedz co masz do powiedzenia, a Gaggle wpisze to na "
            + "czacie w Condorze. Mowa jest rozpoznawana na tym komputerze — nic nie jest "
            + "wysyłane na żaden serwer.",
        Step1Title: "1. Wybierz model mowy",
        Step1Body: "Kliknij prawym przyciskiem ikonę w zasobniku → „Speech model”. „Base” to "
            + "dobry początek: pobiera się raz i potem działa bez internetu. Dopóki nie ma "
            + "modelu, nic nie zadziała.",
        Step2Title: "2. Wybierz mikrofon",
        Step2Body: "Prawy przycisk na ikonie → „Microphone”. Wybierz ten, z którego naprawdę "
            + "latasz — zwykle mikrofon w słuchawkach.",
        Step3Title: "3. Ustaw push-to-talk",
        Step3Body: "Prawy przycisk → „Push-to-talk…”, naciśnij „Change”, a potem klawisz albo "
            + "przycisk joysticka, którego chcesz używać. Klawisze są ukrywane przed Condorem, "
            + "przyciski joysticka nie — wybierz taki, którego symulator jeszcze nie używa.",
        Step4Title: "4. W locie",
        Step4Body: "Przytrzymaj przycisk, mów, puść. To, co usłyszało, pojawi się na pasku "
            + "przy dole ekranu: Enter wysyła, Escape odrzuca. W VR tego paska nie widać, więc "
            + "włącz tryb hands-free w menu ikony — wtedy wiadomości idą od razu.",
        EtiquetteTitle: "Zanim wystartujesz",
        EtiquetteBody: "To pisze na czacie, który widzą wszyscy w wyścigu. Rozpoznawanie mowy "
            + "nigdy nie jest idealne, więc czytaj co usłyszało, zanim wyślesz — przynajmniej "
            + "dopóki mu nie zaufasz. Jest też limit częstotliwości wiadomości i jest tam celowo.",
        Footer: "Wszystko jest pod prawym przyciskiem na ikonie w zasobniku: ustawienia, ten "
            + "przewodnik i „Report a bug…”.",
        LanguageLabel: "Język",
        CloseButton: "Zamknij");

    public static OnboardingText German { get; } = new(
        LanguageName: "Deutsch",
        WindowTitle: "Erste Schritte mit Gaggle",
        Heading: "Willkommen bei Gaggle",
        Intro: "Taste halten, etwas sagen — Gaggle tippt es in den Chat von Condor. Die "
            + "Spracherkennung läuft auf diesem Rechner; nichts wird an einen Server geschickt.",
        Step1Title: "1. Sprachmodell wählen",
        Step1Body: "Rechtsklick auf das Symbol im Infobereich → „Speech model“. „Base“ ist ein "
            + "guter Anfang: einmal heruntergeladen, läuft es danach offline. Ohne Modell "
            + "passiert nichts.",
        Step2Title: "2. Mikrofon wählen",
        Step2Body: "Rechtsklick auf das Symbol → „Microphone“, und nimm das, mit dem du "
            + "wirklich fliegst — meist das im Headset.",
        Step3Title: "3. Push-to-talk festlegen",
        Step3Body: "Rechtsklick → „Push-to-talk…“, auf „Change“ drücken, dann die gewünschte "
            + "Taste oder Joystick-Taste betätigen. Tastatureingaben werden vor Condor "
            + "verborgen, Joystick-Tasten nicht — nimm also eine, die der Simulator noch "
            + "nicht belegt.",
        Step4Title: "4. Im Flug",
        Step4Body: "Taste halten, sprechen, loslassen. Was verstanden wurde, erscheint in "
            + "einem Streifen am unteren Bildrand: Enter sendet, Escape verwirft. In VR ist "
            + "dieser Streifen nicht zu sehen — schalte dort im Menü „hands-free“ ein, dann "
            + "gehen die Nachrichten sofort raus.",
        EtiquetteTitle: "Bevor du fliegst",
        EtiquetteBody: "Das schreibt in einen Chat, den alle im Rennen mitlesen. "
            + "Spracherkennung ist nie perfekt: Lies mit, was verstanden wurde, bevor du "
            + "sendest — jedenfalls solange, bis du ihr traust. Es gibt ein Sendelimit, und "
            + "das ist Absicht.",
        Footer: "Alles steckt im Rechtsklickmenü des Symbols: die Einstellungen, diese "
            + "Anleitung und „Report a bug…“.",
        LanguageLabel: "Sprache",
        CloseButton: "Schließen");

    public static OnboardingText Spanish { get; } = new(
        LanguageName: "Español",
        WindowTitle: "Primeros pasos con Gaggle",
        Heading: "Bienvenido a Gaggle",
        Intro: "Mantén pulsada una tecla, di algo y Gaggle lo escribe en el chat de Condor. "
            + "La transcripción se hace en este ordenador: no se envía nada a ningún servidor.",
        Step1Title: "1. Elige un modelo de voz",
        Step1Body: "Haz clic derecho en el icono del área de notificación → «Speech model». "
            + "«Base» es un buen comienzo: se descarga una vez y luego funciona sin conexión. "
            + "Hasta que no haya un modelo, no funciona nada.",
        Step2Title: "2. Elige el micrófono",
        Step2Body: "Clic derecho en el icono → «Microphone», y elige el que usas de verdad al "
            + "volar, normalmente el de los auriculares.",
        Step3Title: "3. Configura el push-to-talk",
        Step3Body: "Clic derecho → «Push-to-talk…», pulsa «Change» y después la tecla o el "
            + "botón del joystick que quieras. Las teclas se ocultan a Condor; los botones del "
            + "joystick no, así que elige uno que el simulador no use ya.",
        Step4Title: "4. En vuelo",
        Step4Body: "Mantén el botón, habla y suéltalo. Lo que ha entendido aparece en una "
            + "franja en la parte inferior de la pantalla: Enter lo envía, Escape lo descarta. "
            + "En VR esa franja no se ve, así que activa «hands-free» en el menú y los mensajes "
            + "salen directamente.",
        EtiquetteTitle: "Antes de volar",
        EtiquetteBody: "Esto escribe en un chat que lee todo el mundo en la carrera. La "
            + "transcripción nunca es perfecta, así que lee lo que ha entendido antes de "
            + "enviarlo, al menos hasta que te fíes. Hay un límite de frecuencia, y está "
            + "puesto a propósito.",
        Footer: "Todo está en el menú del icono: los ajustes, esta guía y «Report a bug…».",
        LanguageLabel: "Idioma",
        CloseButton: "Cerrar");
}
