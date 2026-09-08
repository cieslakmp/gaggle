using System.Reflection;
using Gaggle.Localisation;
using Gaggle.Speech;

namespace Gaggle.Tests;

/// <summary>
/// The interface is translated by hand, in one table, and the way that goes wrong is
/// quiet: a member added in English and forgotten in Polish still compiles, still runs,
/// and simply shows English to someone who asked for Polish. There is no resource file
/// to diff and no build step to notice.
///
/// So these tests walk the table itself rather than naming strings one by one. A new
/// member is covered the moment it is written, which is the only way this stays honest.
/// </summary>
public class StringsTests
{
    /// <summary>Words that are genuinely the same in both, and are not oversights.</summary>
    private static readonly HashSet<string> SameInBothLanguages = ["Ok"];

    /// <summary>
    /// The tooltip Windows will not take more than 63 characters of, and the prefix
    /// RefreshStatus puts in front of every status line.
    /// </summary>
    private const int TrayTextLimit = 63;

    private const string TrayPrefix = "Gaggle 1.10.2 — ";

    [Fact]
    public void EveryPlainStringIsWrittenInBothLanguages()
    {
        foreach (PropertyInfo property in TextProperties())
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                var value = (string?)property.GetValue(Strings.For(language));

                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{property.Name} is empty in {language}.");
            }
        }
    }

    [Fact]
    public void EveryFormattedStringIsWrittenInBothLanguages()
    {
        foreach (MethodInfo method in TextMethods())
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                var value = (string?)method.Invoke(Strings.For(language), SampleArguments(method));

                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{method.Name} is empty in {language}.");
            }
        }
    }

    /// <summary>
    /// The failure this is really looking for: a member copied into the table with the
    /// English text pasted into both slots, which reads as a working translation and is
    /// not one.
    /// </summary>
    [Fact]
    public void PolishIsActuallyDifferentFromEnglish()
    {
        foreach (PropertyInfo property in TextProperties())
        {
            if (SameInBothLanguages.Contains(property.Name))
            {
                continue;
            }

            var english = (string?)property.GetValue(Strings.For(UiLanguage.English));
            var polish = (string?)property.GetValue(Strings.For(UiLanguage.Polish));

            Assert.True(
                english != polish,
                $"{property.Name} is the same text in both languages. If that is right, "
                    + "add it to SameInBothLanguages.");
        }
    }

    /// <summary>
    /// ModelName and ModelNotes fall back to the English text in ModelInstaller for a
    /// file they do not recognise, so a model added to the menu without a translation
    /// shows English in a Polish menu rather than failing.
    /// </summary>
    [Fact]
    public void EveryModelOnTheMenuIsNamedAndDescribedInBothLanguages()
    {
        Strings english = Strings.For(UiLanguage.English);
        Strings polish = Strings.For(UiLanguage.Polish);

        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            Assert.True(
                english.ModelName(choice.FileName) != polish.ModelName(choice.FileName),
                $"{choice.FileName} has no Polish name; it is falling back to English.");

            Assert.True(
                english.ModelNotes(choice.FileName) != polish.ModelNotes(choice.FileName),
                $"{choice.FileName} has no Polish note; it is falling back to English.");
        }
    }

    /// <summary>
    /// The two rows the speech-language menu offers are prose and are translated. The
    /// per-language endonyms behind them are not, and must not be: "Polski" is "Polski"
    /// whatever the interface is written in.
    /// </summary>
    [Fact]
    public void BothSpeechLanguageChoicesAreTranslatedButEndonymsAreNot()
    {
        Strings english = Strings.For(UiLanguage.English);
        Strings polish = Strings.For(UiLanguage.Polish);

        foreach (SpokenLanguage choice in SpokenLanguage.MenuChoices)
        {
            Assert.True(
                english.SpokenLanguageName(choice.Code) != polish.SpokenLanguageName(choice.Code),
                $"The menu row for {choice.Code} is not translated.");
        }

        Assert.Equal("Polski", english.SpokenLanguageName("pl"));
        Assert.Equal("Polski", polish.SpokenLanguageName("pl"));
        Assert.Equal("Deutsch", polish.SpokenLanguageName("de"));
    }

    /// <summary>
    /// NotifyIcon.Text throws above 63 characters, so RefreshStatus truncates — silently,
    /// which is the problem. Polish runs longer than English, so the thing worth pinning
    /// is that it never pushes a line over the limit that English kept under it.
    ///
    /// One line is over in both languages already: "any language" spelled out in full,
    /// plus the model complaint, does not fit in either. That one loses its tail in the
    /// tooltip and is shown whole in the menu item, which is not truncated.
    /// </summary>
    [Theory]
    [InlineData("hook")]
    [InlineData("english-needs-multilingual")]
    [InlineData("auto-needs-multilingual")]
    [InlineData("no-model")]
    [InlineData("waiting")]
    [InlineData("ready-key")]
    [InlineData("ready-joystick")]
    public void PolishNeverOverrunsATooltipEnglishWouldHaveFitted(string state)
    {
        string english = TrayPrefix + StatusLine(Strings.For(UiLanguage.English), state);
        string polish = TrayPrefix + StatusLine(Strings.For(UiLanguage.Polish), state);

        if (english.Length > TrayTextLimit)
        {
            // Already truncated in English; Polish cannot make that worse than it is.
            return;
        }

        Assert.True(
            polish.Length <= TrayTextLimit,
            $"\"{polish}\" is {polish.Length} characters and will be cut at {TrayTextLimit}, "
                + $"where English fits at {english.Length}.");
    }

    /// <summary>Composed the way RefreshStatus composes it.</summary>
    private static string StatusLine(Strings text, string state) => state switch
    {
        "hook" => text.StatusHookNotInstalled,
        "english-needs-multilingual" =>
            text.StatusNeedsMultilingualModel(text.SpokenLanguageName(SpokenLanguage.EnglishCode)),
        "auto-needs-multilingual" =>
            text.StatusNeedsMultilingualModel(text.SpokenLanguageName(SpokenLanguage.AutoCode)),
        "no-model" => text.StatusNoSpeechModel,
        "waiting" => text.StatusWaitingFor("Condor"),
        "ready-key" => text.StatusReady("Caps Lock"),
        "ready-joystick" => text.StatusReady("Stick 1 · button 12"),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown status line."),
    };

    private static IEnumerable<PropertyInfo> TextProperties() =>
        typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string));

    private static IEnumerable<MethodInfo> TextMethods() =>
        typeof(Strings)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType == typeof(Strings))
            .Where(method => !method.IsSpecialName && method.ReturnType == typeof(string));

    /// <summary>
    /// Every formatted string takes strings and counts, so a placeholder can be filled
    /// without knowing which member is being called.
    /// </summary>
    private static object?[] SampleArguments(MethodInfo method) =>
        [.. method.GetParameters().Select(
            parameter => parameter.ParameterType == typeof(int) ? (object?)1 : "X")];
}
