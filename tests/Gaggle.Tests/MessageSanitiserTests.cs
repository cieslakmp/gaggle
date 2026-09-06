using Gaggle.Text;

namespace Gaggle.Tests;

/// <summary>
/// The sanitiser is the last thing standing between raw Whisper output and a live
/// race chat, so these tests are mostly about what must *not* get through.
/// </summary>
public class MessageSanitiserTests
{
    private const int MaxLength = 120;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void RejectsEmptyInput(string? input)
    {
        Assert.Null(MessageSanitiser.Clean(input, MaxLength));
    }

    [Theory]
    [InlineData("Thank you.")]
    [InlineData("thank you")]
    [InlineData("THANK YOU.")]
    [InlineData("Thanks for watching!")]
    [InlineData("Bye.")]
    [InlineData("you")]
    public void RejectsWhisperHallucinations(string input)
    {
        // Whisper produces these confidently from silence or noise.
        Assert.Null(MessageSanitiser.Clean(input, MaxLength));
    }

    [Theory]
    [InlineData("...")]
    [InlineData(".")]
    [InlineData("?!")]
    [InlineData("-- --")]
    public void RejectsPunctuationOnly(string input)
    {
        Assert.Null(MessageSanitiser.Clean(input, MaxLength));
    }

    [Theory]
    [InlineData("[BLANK_AUDIO]")]
    [InlineData("[ Silence ]")]
    [InlineData("(upbeat music)")]
    public void RejectsAnnotationOnlyOutput(string input)
    {
        Assert.Null(MessageSanitiser.Clean(input, MaxLength));
    }

    [Fact]
    public void StripsLeadingAnnotationButKeepsTheSpeech()
    {
        string? result = MessageSanitiser.Clean("(wind blowing) turning left now", MaxLength);

        Assert.Equal("turning left now", result);
    }

    [Fact]
    public void StripsSeveralStackedAnnotations()
    {
        string? result = MessageSanitiser.Clean("[music] (coughs) climbing well here", MaxLength);

        Assert.Equal("climbing well here", result);
    }

    [Fact]
    public void KeepsAnOrdinaryMessageIntact()
    {
        const string message = "Climbing at four knots over the second turnpoint.";

        Assert.Equal(message, MessageSanitiser.Clean(message, MaxLength));
    }

    [Fact]
    public void RemovesNewlinesBecauseEnterWouldSubmitEarly()
    {
        string? result = MessageSanitiser.Clean("turning left\r\nthen climbing", MaxLength);

        Assert.NotNull(result);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain('\r', result);
        Assert.Equal("turning left then climbing", result);
    }

    [Fact]
    public void CollapsesRunsOfWhitespace()
    {
        Assert.Equal("left    turn".Replace("    ", " "), MessageSanitiser.Clean("left    turn", MaxLength));
    }

    [Theory]
    [InlineData("don’t stop", "don't stop")]
    [InlineData("“watch out”", "\"watch out\"")]
    [InlineData("climbing — nicely", "climbing - nicely")]
    [InlineData("wait… now", "wait... now")]
    public void ReplacesCharactersMostLayoutsCannotType(string input, string expected)
    {
        Assert.Equal(expected, MessageSanitiser.Clean(input, MaxLength));
    }

    [Fact]
    public void TruncatesToTheLimit()
    {
        string long_ = string.Join(' ', Enumerable.Repeat("alpha", 60));

        string? result = MessageSanitiser.Clean(long_, MaxLength);

        Assert.NotNull(result);
        Assert.True(result.Length <= MaxLength, $"expected <= {MaxLength}, got {result.Length}");
    }

    [Fact]
    public void TruncationPrefersAWordBoundary()
    {
        string? result = MessageSanitiser.Clean("turning left at the second turnpoint now", 20);

        Assert.Equal("turning left at the", result);
    }

    [Fact]
    public void TruncationDoesNotThrowAwayMostOfAMessage()
    {
        // The only space sits early on, so clipping to it would discard nearly
        // everything; a hard clip is the better trade.
        string? result = MessageSanitiser.Clean("ok abcdefghijklmnopqrstuvwxyz", 20);

        Assert.NotNull(result);
        Assert.True(result.Length > 10, $"lost too much of the message: '{result}'");
    }

    [Fact]
    public void ShortMessagesAreNotTruncated()
    {
        Assert.Equal("left turn", MessageSanitiser.Clean("left turn", 120));
    }

    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("plain ascii", "plain ascii")]
    public void FoldToAsciiStripsDiacritics(string input, string expected)
    {
        Assert.Equal(expected, MessageSanitiser.FoldToAscii(input));
    }
}
