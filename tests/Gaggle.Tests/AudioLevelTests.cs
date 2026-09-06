using Gaggle.Audio;
using NAudio.Wave;

namespace Gaggle.Tests;

/// <summary>
/// The RMS gate is what stops Whisper being fed silence, which is when it invents
/// stock phrases. These tests pin the measurement rather than the threshold.
/// </summary>
public class AudioLevelTests
{
    private const int SampleRate = 16_000;

    [Fact]
    public void SilenceMeasuresAsZero()
    {
        using MemoryStream wav = CreateWav(amplitude: 0f, seconds: 0.2);

        Assert.Equal(0d, MicrophoneRecorder.CalculateRms(wav), 4);
    }

    [Fact]
    public void ConstantToneMeasuresAtItsAmplitude()
    {
        using MemoryStream wav = CreateWav(amplitude: 0.5f, seconds: 0.2);

        Assert.Equal(0.5d, MicrophoneRecorder.CalculateRms(wav), 2);
    }

    [Fact]
    public void QuietAudioFallsBelowTheDefaultThreshold()
    {
        // 0.001 is the kind of level an open mic in a quiet room produces.
        using MemoryStream wav = CreateWav(amplitude: 0.001f, seconds: 0.2);

        double rms = MicrophoneRecorder.CalculateRms(wav);

        Assert.True(rms < 0.005, $"expected below the 0.005 gate, measured {rms}");
    }

    [Fact]
    public void SpeechLevelAudioPassesTheDefaultThreshold()
    {
        using MemoryStream wav = CreateWav(amplitude: 0.1f, seconds: 0.2);

        double rms = MicrophoneRecorder.CalculateRms(wav);

        Assert.True(rms > 0.005, $"expected above the 0.005 gate, measured {rms}");
    }

    [Fact]
    public void LeavesTheStreamPositionWhereItFoundIt()
    {
        // The caller hands the same stream to Whisper afterwards.
        using MemoryStream wav = CreateWav(amplitude: 0.3f, seconds: 0.1);
        wav.Position = 0;

        MicrophoneRecorder.CalculateRms(wav);

        Assert.Equal(0, wav.Position);
    }

    [Fact]
    public void AnEmptyRecordingMeasuresAsZeroRatherThanThrowing()
    {
        using MemoryStream wav = CreateWav(amplitude: 0f, seconds: 0);

        Assert.Equal(0d, MicrophoneRecorder.CalculateRms(wav), 4);
    }

    /// <summary>Builds a 16 kHz mono WAV holding a constant amplitude.</summary>
    private static MemoryStream CreateWav(float amplitude, double seconds)
    {
        var stream = new MemoryStream();
        var writer = new WaveFileWriter(stream, new WaveFormat(SampleRate, 16, 1));

        for (int i = 0; i < (int)(SampleRate * seconds); i++)
        {
            writer.WriteSample(amplitude);
        }

        // Flush updates the header without disposing the underlying stream.
        writer.Flush();
        stream.Position = 0;

        return stream;
    }
}
