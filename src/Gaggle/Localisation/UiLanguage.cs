namespace Gaggle.Localisation;

/// <summary>
/// The language of Gaggle's own menus and dialogs.
///
/// Not to be confused with <see cref="Configuration.AppConfig.Language"/>, which is the
/// language the pilot <em>speaks</em> into the microphone. The two are independent: a
/// Polish pilot may well want a Polish interface and an English-only speech model, which
/// is the faster pairing when they call in English.
/// </summary>
public enum UiLanguage
{
    English,
    Polish,
}
