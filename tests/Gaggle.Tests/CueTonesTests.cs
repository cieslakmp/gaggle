using System.Text;
using Gaggle.Audio;

namespace Gaggle.Tests;

/// <summary>
/// The cue tones are the only feedback a pilot in a headset gets, and every way they can
/// go wrong is quiet. SoundPlayer declines a buffer it cannot parse without raising
/// anything, so a header field that is off by four bytes is simply silence — and silence
/// is also what "nothing was sent" sounds like.
///
/// The playback itself needs a sound card and is checked by hand. The buffer does not.
/// </summary>
public class CueTonesTests
{
    private const int SampleRate = 16000;

    [Fact]
    public void BuildsAWavHeaderSoundPlayerWillAccept()
    {
        byte[] wav = CueTones.BuildTone(1000, 100);

        Assert.Equal("RIFF", Ascii(wav, 0));
        Assert.Equal("WAVE", Ascii(wav, 8));
        Assert.Equal("fmt ", Ascii(wav, 12));
        Assert.Equal("data", Ascii(wav, 36));

        Assert.Equal(16, BitConverter.ToInt32(wav, 16));           // PCM chunk size
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));            // format: PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));            // mono
        Assert.Equal(SampleRate, BitConverter.ToInt32(wav, 24));
        Assert.Equal(SampleRate * 2, BitConverter.ToInt32(wav, 28)); // byte rate
        Assert.Equal(2, BitConverter.ToInt16(wav, 32));            // block align
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));           // bits per sample
    }

    [Fact]
    public void DeclaredLengthsMatchTheBuffer()
    {
        byte[] wav = CueTones.BuildTone(1000, 100);

        // The two fields that are wrong in every hand-rolled WAV writer. Either one
        // being wrong plays nothing, with no error to say so.
        Assert.Equal(wav.Length - 8, BitConverter.ToInt32(wav, 4));
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(160)]
    public void HoldsAsManySamplesAsTheDurationAsksFor(int milliseconds)
    {
        byte[] wav = CueTones.BuildTone(1000, milliseconds);

        Assert.Equal(44 + (SampleRate * milliseconds / 1000 * 2), wav.Length);
    }

    [Fact]
    public void FadesInAndOutSoTheToneDoesNotClick()
    {
        byte[] wav = CueTones.BuildTone(1000, 100);

        Assert.Equal(0, BitConverter.ToInt16(wav, 44));
        Assert.Equal(0, BitConverter.ToInt16(wav, wav.Length - 2));
    }

    [Fact]
    public void StaysWellInsideTheSampleRange()
    {
        // Amplitude is deliberately low: a cue that startles somebody mid-thermal is
        // worse than no cue at all.
        byte[] wav = CueTones.BuildTone(1000, 100);

        for (int offset = 44; offset < wav.Length; offset += 2)
        {
            short sample = BitConverter.ToInt16(wav, offset);

            Assert.InRange(sample, (short)(short.MinValue / 3), (short)(short.MaxValue / 3));
        }
    }

    [Fact]
    public void TheThreeCuesAreActuallyDifferentSounds()
    {
        // "Distinct tones" is the whole requirement. Two cues built from the same pair
        // of numbers would pass every other test here and be useless in a headset.
        Assert.NotEqual(CueTones.StartedWav, CueTones.SentWav);
        Assert.NotEqual(CueTones.SentWav, CueTones.DroppedWav);
        Assert.NotEqual(CueTones.StartedWav, CueTones.DroppedWav);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(1000, 0)]
    [InlineData(1000, -5)]
    public void RefusesAToneThatCannotExist(int frequencyHz, int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CueTones.BuildTone(frequencyHz, milliseconds));
    }

    private static string Ascii(byte[] wav, int offset) => Encoding.ASCII.GetString(wav, offset, 4);
}
