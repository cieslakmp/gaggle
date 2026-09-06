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
    public void TheDefaultModelSuitsTheDefaultLanguage()
    {
        var config = new AppConfig();

        Assert.True(
            ModelInstaller.IsMultilingualFile(config.WhisperModelFile)
                || SpokenLanguage.IsEnglish(config.Language),
            "the shipped defaults must not be the combination the app refuses to load");
    }
}
