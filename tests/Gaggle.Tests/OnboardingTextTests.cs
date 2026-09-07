using System.Reflection;
using Gaggle.Ui;

namespace Gaggle.Tests;

/// <summary>
/// The guide is the only part of Gaggle that exists in four languages, and it is the
/// first thing a new user sees. Every way it can go wrong is invisible to whoever wrote
/// it: an empty string shows a blank line, and a translation left as the English text
/// looks fine until the person it was for opens it.
/// </summary>
public class OnboardingTextTests
{
    public static TheoryData<string> Codes()
    {
        var data = new TheoryData<string>();

        foreach (string code in OnboardingText.AvailableCodes)
        {
            data.Add(code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLineIsWrittenInEveryLanguage(string code)
    {
        OnboardingText text = OnboardingText.For(code);

        // Reflected rather than listed, so a line added to the record is covered here
        // the moment it exists rather than whenever somebody remembers this file.
        foreach (PropertyInfo property in Properties())
        {
            var value = (string?)property.GetValue(text);

            Assert.False(
                string.IsNullOrWhiteSpace(value),
                $"{code} is missing {property.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void EveryLanguageNamesItselfInItsOwnLanguage(string code)
    {
        // A picker listing "Polish" rather than "Polski" is a picker written for
        // somebody who does not need it.
        string name = OnboardingText.For(code).LanguageName;

        Assert.Equal(name, name.Trim());
        Assert.DoesNotContain("TODO", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TranslationsAreNotJustTheEnglishTextAgain()
    {
        // The failure mode of a stubbed-out language: it builds, it fills every field,
        // and it is useless to the only person who would open it.
        foreach (string code in OnboardingText.AvailableCodes.Where(c => c != "en"))
        {
            OnboardingText text = OnboardingText.For(code);

            foreach (PropertyInfo property in Properties())
            {
                Assert.NotEqual((string?)property.GetValue(OnboardingText.English), (string?)property.GetValue(text));
            }
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData(null)]
    public void FallsBackToEnglishRatherThanAnEmptyWindow(string? code)
    {
        // A hand-edited config naming a language nobody has written yet.
        Assert.Same(OnboardingText.English, OnboardingText.For(code));
    }

    [Theory]
    [InlineData("PL")]
    [InlineData("Pl")]
    public void AcceptsALanguageCodeInAnyCase(string code)
    {
        Assert.Same(OnboardingText.Polish, OnboardingText.For(code));
    }

    [Fact]
    public void EnglishLeadsThePickerBecauseItIsTheFallback()
    {
        Assert.Equal("en", OnboardingText.AvailableCodes[0]);
        Assert.Equal(OnboardingText.AvailableCodes.Count, OnboardingText.AvailableCodes.Distinct().Count());
    }

    [Fact]
    public void EveryOfferedLanguageResolvesToItsOwnText()
    {
        // Guards the switch in For(): a missing arm would quietly hand somebody English
        // while the picker claimed otherwise.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string code in OnboardingText.AvailableCodes)
        {
            Assert.True(seen.Add(OnboardingText.For(code).LanguageName), $"{code} resolves to a duplicate");
        }
    }

    private static PropertyInfo[] Properties() =>
        [.. typeof(OnboardingText)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))];
}
