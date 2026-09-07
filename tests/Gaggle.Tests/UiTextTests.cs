using System.Reflection;
using Gaggle.Ui;

namespace Gaggle.Tests;

/// <summary>
/// The interface exists in four languages and only one of them is read by whoever adds a
/// string. The compiler catches a missing member, because they are <c>required</c>; it
/// cannot catch the three faults below, and every one of them is invisible in English.
///
/// Members are found by reflection rather than listed, so a string added to
/// <see cref="UiText"/> is covered the moment it exists rather than whenever somebody
/// remembers this file.
/// </summary>
public class UiTextTests
{
    /// <summary>Stand-ins for whatever the app would pass: a process name, a version.</summary>
    private static readonly object[] OneArgument = ["ARGUMENT-ONE"];

    private static readonly object[] TwoArguments = ["ARGUMENT-ONE", "ARGUMENT-TWO"];

    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();

        foreach (string code in UiLanguages.Codes)
        {
            data.Add(code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryStringIsWrittenInEveryLanguage(string code)
    {
        UiText text = UiText.For(code);

        foreach (PropertyInfo property in Members())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(Render(property, text)),
                $"{code} is missing {property.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryParameterReachesTheTextThatUsesIt(string code)
    {
        // The fault a compiler cannot see: a translation that takes the argument and
        // then does not put it anywhere. "Condor is not running." would become a bare
        // "nie jest uruchomiony." and nobody who reads English would ever notice.
        UiText text = UiText.For(code);

        foreach (PropertyInfo property in Members().Where(IsParameterised))
        {
            string rendered = Render(property, text);
            int expected = property.PropertyType.GetGenericArguments().Length - 1;

            for (int i = 0; i < expected; i++)
            {
                string argument = i == 0 ? "ARGUMENT-ONE" : "ARGUMENT-TWO";

                Assert.True(
                    rendered.Contains(argument, StringComparison.Ordinal),
                    $"{code} drops argument {i + 1} of {property.Name}: \"{rendered}\"");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void NoTranslationIsJustTheEnglishTextAgain(string code)
    {
        // The fault that builds, fills every member, and is useless to the only person
        // who would ever read it. A handful of strings are the same word in every
        // language, so this checks the language as a whole rather than each member.
        if (code == "en")
        {
            return;
        }

        UiText text = UiText.For(code);
        PropertyInfo[] members = Members();
        int translated = members.Count(p => Render(p, text) != Render(p, UiText.English));

        Assert.True(
            translated > members.Length * 9 / 10,
            $"{code} leaves {members.Length - translated} of {members.Length} strings in English");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nonsense")]
    public void FallsBackToEnglishRatherThanNothing(string? code)
    {
        Assert.Same(UiText.English, UiText.For(code));
    }

    [Theory]
    [InlineData("PL")]
    [InlineData("  pl  ")]
    public void AcceptsALanguageCodeInAnyCaseOrPadding(string code)
    {
        Assert.Same(UiText.Polish, UiText.For(code));
    }

    [Fact]
    public void CurrentStartsInEnglishAndFollowsUse()
    {
        // Current is backed by a field on purpose: a property initialiser would run
        // before the language tables and assign null.
        Assert.NotNull(UiText.Current);

        try
        {
            UiText.Use("de");
            Assert.Same(UiText.German, UiText.Current);

            UiText.Use("zz");
            Assert.Same(UiText.English, UiText.Current);
        }
        finally
        {
            UiText.Use(UiLanguages.Default);
        }
    }

    [Fact]
    public void EveryOfferedLanguageHasBothATableAndAName()
    {
        // The display-language menu is built from UiLanguages.Codes, so a code listed
        // there without text behind it would offer the reader English under their own
        // language's name.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string code in UiLanguages.Codes)
        {
            Assert.False(string.IsNullOrWhiteSpace(UiLanguages.NameOf(code)));
            Assert.True(seen.Add(UiText.For(code).MenuExit), $"{code} shares its text with another language");
        }

        Assert.Equal(UiLanguages.Default, UiLanguages.Codes[0]);
    }

    private static PropertyInfo[] Members() =>
        [.. typeof(UiText)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) || IsParameterised(p))];

    private static bool IsParameterised(PropertyInfo property) =>
        property.PropertyType == typeof(Func<string, string>)
        || property.PropertyType == typeof(Func<string, string, string>);

    /// <summary>The member as a reader would see it, with stand-ins for any arguments.</summary>
    private static string Render(PropertyInfo property, UiText text)
    {
        object? value = property.GetValue(text);

        if (value is string plain)
        {
            return plain;
        }

        var target = (Delegate)value!;
        object[] arguments = target.Method.GetParameters().Length == 2 ? TwoArguments : OneArgument;

        return (string)target.DynamicInvoke(arguments)!;
    }
}
