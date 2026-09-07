namespace Gaggle.Speech;

/// <summary>
/// A language the pilot can speak. Whisper transcribes ~99 of them, but only these
/// are offered: a longer list would be a scrolling tray menu nobody wants mid-flight.
///
/// Anything other than English is translated to English on the way out, because the
/// chat is shared and English is the common language. Whisper only translates
/// <em>into</em> English, so this list can never grow an output language.
/// </summary>
/// <param name="Code">The two-letter code Whisper expects, or "auto".</param>
/// <param name="Name">Endonym, shown in the tray menu.</param>
public sealed record SpokenLanguage(string Code, string Name)
{
    /// <summary>The code meaning "work it out from the audio".</summary>
    public const string AutoCode = "auto";

    /// <summary>The one language that needs no translation.</summary>
    public const string EnglishCode = "en";

    /// <summary>
    /// Every code the app understands. Used to name a setting, not to build the menu.
    ///
    /// The per-language codes are here for the tail case where detection picks wrong
    /// and you want to pin the language by hand in config.json. They are deliberately
    /// off the menu: with translation on, "pl", "de" and "es" all decode to English
    /// and produce near-identical output, so offering them as a choice implies a
    /// decision that is not really being made.
    /// </summary>
    public static readonly IReadOnlyList<SpokenLanguage> Available =
    [
        new(EnglishCode, "English"),
        new("pl", "Polski"),
        new("de", "Deutsch"),
        new("es", "Español"),
        new(AutoCode, "Detect automatically"),
    ];

    /// <summary>
    /// What the tray actually offers. Two states, because two is what there is:
    /// English, transcribed by a dedicated English model, or anything at all,
    /// detected and translated into English by a multilingual one.
    /// </summary>
    public static readonly IReadOnlyList<SpokenLanguage> MenuChoices =
    [
        new(EnglishCode, "English"),
        new(AutoCode, "Any language → English"),
    ];

    /// <summary>
    /// True when speech in this language reaches chat unchanged. Everything else,
    /// "auto" included, goes through Whisper's translate task — under auto-detection
    /// the spoken language is not known until decoding, so translation has to be on.
    /// </summary>
    public static bool IsEnglish(string code) =>
        string.Equals(code, EnglishCode, StringComparison.OrdinalIgnoreCase);

    public static bool IsAuto(string code) =>
        string.Equals(code, AutoCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>The display name for a code, falling back to the code itself.</summary>
    public static string Describe(string code)
    {
        foreach (SpokenLanguage language in Available)
        {
            if (string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return language.Name;
            }
        }

        return code;
    }
}
