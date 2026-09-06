using NAudio.Wave;

namespace Gaggle.Audio;

/// <summary>
/// Captures the microphone straight into the format Whisper wants: 16 kHz, mono,
/// 16-bit PCM. Recording that natively avoids a resample step on every message.
/// </summary>
public sealed class MicrophoneRecorder : IDisposable
{
    private const int SampleRate = 16_000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    private readonly object _sync = new();
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;

    public bool IsRecording { get; private set; }

    /// <summary>Raised if NAudio fails mid-capture (device unplugged, for example).</summary>
    public event Action<Exception>? Failed;

    public static IReadOnlyList<string> ListDevices()
    {
        var devices = new List<string>();

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            devices.Add(WaveInEvent.GetCapabilities(i).ProductName);
        }

        return devices;
    }

    public void Start(int deviceIndex)
    {
        lock (_sync)
        {
            if (IsRecording)
            {
                return;
            }

            _buffer = new MemoryStream();
            _writer = new WaveFileWriter(_buffer, new WaveFormat(SampleRate, BitsPerSample, Channels));

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex < 0 ? 0 : deviceIndex,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 50,
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            IsRecording = true;
        }
    }

    /// <summary>
    /// Stops capture and returns a complete WAV stream positioned at zero, or null if
    /// nothing was captured. The caller owns the returned stream.
    /// </summary>
    public MemoryStream? Stop()
    {
        lock (_sync)
        {
            if (!IsRecording)
            {
                return null;
            }

            IsRecording = false;

            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            _waveIn = null;

            if (_writer is null || _buffer is null)
            {
                return null;
            }

            // Flush the WAV header without disposing the underlying MemoryStream.
            _writer.Flush();
            MemoryStream captured = _buffer;
            _writer = null;
            _buffer = null;

            if (captured.Length == 0)
            {
                captured.Dispose();
                return null;
            }

            captured.Position = 0;
            return captured;
        }
    }

    /// <summary>
    /// Root-mean-square amplitude of a 16-bit PCM WAV stream, normalised to 0..1.
    /// Used to reject silence before it reaches Whisper — fed an empty channel,
    /// Whisper reliably hallucinates stock phrases like "Thank you."
    /// </summary>
    public static double CalculateRms(Stream wav)
    {
        long start = wav.Position;

        try
        {
            using var reader = new WaveFileReader(wav) { Position = 0 };
            double sumOfSquares = 0;
            long count = 0;

            float[]? frame;
            while ((frame = reader.ReadNextSampleFrame()) is not null)
            {
                foreach (float sample in frame)
                {
                    sumOfSquares += sample * (double)sample;
                    count++;
                }
            }

            return count == 0 ? 0 : Math.Sqrt(sumOfSquares / count);
        }
        finally
        {
            wav.Position = start;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_sync)
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            Failed?.Invoke(e.Exception);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _waveIn?.Dispose();
            _waveIn = null;
            _writer = null;
            _buffer?.Dispose();
            _buffer = null;
            IsRecording = false;
        }
    }
}
