using Gaggle.Configuration;
using Gaggle.Speech;

namespace Gaggle.Tests;

/// <summary>
/// The language list and the model list have to agree with each other, because the
/// combination that fails — a non-English language on an English-only model — fails
/// silently, by transcribing confident nonsense rather than erroring.
/// </summary>
public class SpeechLanguageTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("pl")]
    [InlineData("de")]
    [InlineData("es")]
    public void OffersTheLanguagesTheUserAskedFor(string code)
    {
        Assert.Contains(SpokenLanguage.Available, language => language.Code == code);
    }

    [Fact]
    public void CodesAreTheTwoLetterFormWhisperExpects()
    {
        foreach (SpokenLanguage language in SpokenLanguage.Available)
        {
            if (SpokenLanguage.IsAuto(language.Code))
            {
                continue;
            }

            Assert.Equal(2, language.Code.Length);
            Assert.Equal(language.Code.ToLowerInvariant(), language.Code);
        }
    }

    [Fact]
    public void TheDefaultConfigLanguageIsOneWeOffer()
    {
        var config = new AppConfig();

        Assert.Contains(SpokenLanguage.Available, language => language.Code == config.Language);
    }

    [Theory]
    [InlineData("en", false)]
    [InlineData("EN", false)]
    [InlineData("pl", true)]
    [InlineData("de", true)]
    [InlineData("es", true)]
    [InlineData("auto", true)]
    public void TranslatesEverythingExceptEnglish(string code, bool expected)
    {
        // "auto" translates too: under detection the spoken language is not known
        // until decoding has already started, so the translate task has to be on.
        var options = new TranscriptionOptions { Language = code };

        Assert.Equal(expected, options.TranslateToEnglish);
    }

    [Fact]
    public void DescribesAKnownCodeAndFallsBackToTheCodeItself()
    {
        Assert.Equal("Polski", SpokenLanguage.Describe("pl"));
        Assert.Equal("fr", SpokenLanguage.Describe("fr"));
    }

    [Theory]
    [InlineData("ggml-tiny.en.bin", false)]
    [InlineData("ggml-base.en.bin", false)]
    [InlineData("ggml-small.en.bin", false)]
    [InlineData("ggml-small.bin", true)]
    [InlineData("ggml-medium.bin", true)]
    [InlineData("ggml-large-v3.bin", true)]
    public void TellsMultilingualBuildsFromEnglishOnlyOnes(string fileName, bool expected)
    {
        Assert.Equal(expected, ModelInstaller.IsMultilingualFile(fileName));
    }

    [Fact]
    public void EveryOfferedModelAgreesWithItsOwnFileName()
    {
        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            Assert.Equal(ModelInstaller.IsMultilingualFile(choice.FileName), choice.IsMultilingual);
        }
    }

    [Fact]
    public void AtLeastOneMultilingualModelIsOfferedForDownload()
    {
        // Without one, picking Polish from the tray leaves the user with no way to
        // satisfy the multilingual check.
        Assert.Contains(ModelInstaller.Available, choice => choice.IsMultilingual);
    }

    [Fact]
    public void TheMenuOffersExactlyTheTwoStatesThatExist()
    {
        // "pl", "de" and "es" all decode to English and produce near-identical output,
        // so the menu offers the decision actually being made: English, or anything.
        Assert.Equal(2, SpokenLanguage.MenuChoices.Count);
        Assert.Contains(SpokenLanguage.MenuChoices, l => SpokenLanguage.IsEnglish(l.Code));
        Assert.Contains(SpokenLanguage.MenuChoices, l => SpokenLanguage.IsAuto(l.Code));
    }

    [Fact]
    public void EveryMenuChoiceIsAKnownLanguage()
    {
        foreach (SpokenLanguage choice in SpokenLanguage.MenuChoices)
        {
            Assert.Contains(SpokenLanguage.Available, known => known.Code == choice.Code);
        }
    }

    [Theory]
    [InlineData("ggml-small.bin", "ggml-small.en.bin")]
    [InlineData("ggml-base.bin", "ggml-base.en.bin")]
    public void SwitchingToEnglishKeepsTheSameTierWhenThereIsOne(string multilingual, string expected)
    {
        Assert.Equal(expected, ModelInstaller.CounterpartEnglish(multilingual).FileName);
    }

    [Fact]
    public void SwitchingToEnglishFromATierWithNoEnglishBuildDoesNotDropToTiny()
    {
        // Medium has no ".en" counterpart on the menu. Falling back to the smallest
        // English build would quietly downgrade someone who chose the largest model.
        ModelInstaller.ModelChoice offer = ModelInstaller.CounterpartEnglish("ggml-medium.bin");

        Assert.False(offer.IsMultilingual);
        Assert.Equal("ggml-small.en.bin", offer.FileName);
    }

    [Fact]
    public void EveryCounterpartIsAnEnglishBuildThatIsOffered()
    {
        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            ModelInstaller.ModelChoice offer = ModelInstaller.CounterpartEnglish(choice.FileName);

            Assert.False(offer.IsMultilingual, $"{choice.FileName} mapped to a multilingual build");
            Assert.Contains(ModelInstaller.Available, c => c.FileName == offer.FileName);
        }
    }

    [Fact]
    public void TheDefaultModelSuitsTheDefaultLanguage()
    {
        var config = new AppConfig();

        Assert.True(
            ModelInstaller.IsMultilingualFile(config.WhisperModelFile)
                || SpokenLanguage.IsEnglish(config.Language),
            "the shipped defaults must not be the combination the app refuses to load");
    }
}
