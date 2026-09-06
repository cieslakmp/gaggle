using Gaggle.Speech;

namespace Gaggle.Tests;

/// <summary>
/// The audio context override is the one speed setting that can damage a transcript,
/// so the sizing is pinned here: never above Whisper's real window, never so small
/// that a short call has nothing left to decode.
/// </summary>
public class TranscriptionOptionsTests
{
    private const int FullWindow = 1500;
    private const int Floor = 512;

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(120)]
    public void StaysWithinTheUsableRange(double seconds)
    {
        int context = TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(seconds));

        Assert.InRange(context, Floor, FullWindow);
    }

    [Fact]
    public void ShortCallsStopShrinkingAtTheFloor()
    {
        Assert.Equal(Floor, TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void AClipFillingTheWindowGetsTheWholeWindow()
    {
        Assert.Equal(FullWindow, TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void AMaximumLengthRecordingStillSavesSomething()
    {
        // MaxRecordingSeconds defaults to 15, half of Whisper's window, so even the
        // longest allowed message should skip a useful chunk of the encoder's work.
        int context = TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(15));

        Assert.True(context < FullWindow, $"expected a saving at 15s, got the full {context}");
    }

    [Fact]
    public void LongerClipsNeverGetLessContext()
    {
        int previous = 0;

        for (double seconds = 0.5; seconds <= 30; seconds += 0.5)
        {
            int context = TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(seconds));

            Assert.True(context >= previous, $"context shrank at {seconds}s");
            previous = context;
        }
    }

    [Fact]
    public void SizesAboveTheClipSoTheLastWordIsNotCutOff()
    {
        // 1500 positions cover 30 seconds, so a 10 second clip needs 500 to be
        // represented at all — and more than that to survive intact.
        int context = TranscriptionOptions.AudioContextFor(TimeSpan.FromSeconds(10));

        Assert.True(context > 500, $"no headroom over the clip length: {context}");
    }

    [Fact]
    public void AnUnknownLengthFallsBackToTheFullWindow()
    {
        Assert.Equal(FullWindow, TranscriptionOptions.AudioContextFor(TimeSpan.Zero));
    }

    [Fact]
    public void DefaultsLeaveWhisperNetsOwnChoicesAlone()
    {
        // Zero means "do not call the setter at all"; Whisper.net's default thread
        // count is every hardware thread, which is the behaviour up to now.
        var options = new TranscriptionOptions { Language = "en" };

        Assert.Equal(0, options.Threads);
        Assert.Equal(0, options.AudioContextSize);
    }
}
