namespace Gaggle.Ui;

/// <summary>
/// The languages the interface exists in.
///
/// One list, shared by <see cref="UiText"/> and <see cref="OnboardingText"/>, so the
/// display-language menu can never offer a language one of them has not been written in.
/// </summary>
public static class UiLanguages
{
    /// <summary>
    /// In the order the menu offers them. English leads because it is both the default
    /// and the fallback for anything unrecognised.
    /// </summary>
    public static IReadOnlyList<string> Codes { get; } = ["en", "pl", "de", "es"];

    public const string Default = "en";

    /// <summary>
    /// The language's own name for itself. A picker listing "Polish" rather than
    /// "Polski" is a picker written for somebody who does not need it.
    /// </summary>
    public static string NameOf(string code) => code switch
    {
        "pl" => "Polski",
        "de" => "Deutsch",
        "es" => "Español",
        _ => "English",
    };

    /// <summary>
    /// A code this app has text for, or <see cref="Default"/>. Guards a hand-edited
    /// config naming a language nobody has written yet.
    /// </summary>
    public static string Normalise(string? code)
    {
        string lower = code?.Trim().ToLowerInvariant() ?? Default;

        return Codes.Contains(lower) ? lower : Default;
    }
}
