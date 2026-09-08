using System.Globalization;

namespace Gaggle.Localisation;

/// <summary>
/// Every user-facing string in the app, in both languages, side by side.
///
/// This is deliberately C# rather than .resx, and the reason is the release pipeline
/// rather than taste. release.yml packages only Gaggle.exe and runtimes/, and
/// <see cref="Update.UpdateInstaller"/> validates only those two paths, so a satellite
/// pl\Gaggle.resources.dll would be dropped from every zip and every in-app update — and
/// a missing satellite does not fail, it falls back to English. That is the one failure
/// mode this app cannot afford: silent.
///
/// Formatted strings are methods rather than format strings for the same reason. A "{0}"
/// that exists in one language and not the other is a runtime surprise in .resx; here it
/// is a compile error.
///
/// The class is an instance rather than a static so a caller can ask for a language
/// without changing anyone else's — <see cref="For"/>. Tests rely on that: xunit runs
/// classes in parallel, and a global that tests reassigned would bleed between them.
/// <see cref="Current"/> is set on the UI thread at startup and on a language change,
/// and read everywhere; a reference read is atomic, so it needs no lock.
/// </summary>
public sealed partial class Strings
{
    private readonly UiLanguage _language;

    private Strings(UiLanguage language) => _language = language;

    /// <summary>The language the app is currently speaking.</summary>
    public static Strings Current { get; private set; } = For(UiLanguage.English);

    /// <summary>A table for one language, without disturbing <see cref="Current"/>.</summary>
    public static Strings For(UiLanguage language) => new(language);

    /// <summary>Switches the app over. Call on the UI thread.</summary>
    public static void Use(UiLanguage language) => Current = For(language);

    /// <summary>Which language this table holds.</summary>
    public UiLanguage Language => _language;

    /// <summary>
    /// The Windows display language, mapped onto what Gaggle can speak. A first run on
    /// Polish Windows should not have to be translated into Polish through an English
    /// menu; everything else starts in English.
    /// </summary>
    public static UiLanguage FromSystem() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl"
            ? UiLanguage.Polish
            : UiLanguage.English;

    /// <summary>The endonym for a language, for the menu that switches between them.</summary>
    public static string NameOf(UiLanguage language) => language switch
    {
        UiLanguage.Polish => "Polski",
        _ => "English",
    };

    private string Pick(string english, string polish) =>
        _language == UiLanguage.Polish ? polish : english;
}
