using NAudio.Wave;

namespace LimbusSplitPro.Core.Audio;

/// <summary>
/// Envuelve un stem decodificado (WAV, PCM float) y permite hacer seek sin
/// producir clics, dobles ataques ni silencios perceptibles (sección 15).
/// Estrategia: en vez de reconstruir el grafo de audio, se aplica una
/// rampa lineal corta (por defecto 8 ms) inmediatamente antes y después del
/// punto de salto, todo dentro del mismo buffer de Read() cuando es
/// posible.
/// </summary>
public sealed class SeekableSampleTrack : ISampleProvider
{
    private const int RampMilliseconds = 8;

    private readonly AudioFileReader _reader;
    private readonly int _rampSamplesPerChannel;
    private long _pendingSeekSample = -1;
    private int _fadeOutRemaining;
    private int _fadeInRemaining;
    private int _fadeInTotal;

    public string StemName { get; }

    public WaveFormat WaveFormat => _reader.WaveFormat;

    /// <summary>Posición actual en samples por canal, para el reloj compartido del mezclador.</summary>
    public long CurrentSamplePosition { get; private set; }

    public long TotalSamples { get; }

    public SeekableSampleTrack(string stemName, string filePath)
    {
        StemName = stemName;
        _reader = new AudioFileReader(filePath);
        _rampSamplesPerChannel = (int)(_reader.WaveFormat.SampleRate * RampMilliseconds / 1000.0);
        TotalSamples = _reader.Length / (_reader.WaveFormat.BitsPerSample / 8) / _reader.WaveFormat.Channels;
    }

    /// <summary>
    /// Pide un seek a una posición absoluta (en samples por canal). Se
    /// aplica en el próximo Read(), con fade-out/fade-in cortos.
    /// </summary>
    public void RequestSeek(long targetSamplePosition)
    {
        _pendingSeekSample = Math.Clamp(targetSamplePosition, 0, TotalSamples);
        _fadeOutRemaining = _rampSamplesPerChannel;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = _reader.WaveFormat.Channels;
        var framesRequested = count / channels;
        var read = _reader.Read(buffer, offset, count);
        var framesRead = read / channels;

        if (_fadeOutRemaining > 0)
        {
            ApplyFadeOut(buffer, offset, framesRead, channels);
        }

        if (_pendingSeekSample >= 0 && _fadeOutRemaining <= 0)
        {
            // Ya se atenuó el final del buffer anterior; ahora sí saltamos.
            _reader.Position = _pendingSeekSample * channels * (_reader.WaveFormat.BitsPerSample / 8);
            CurrentSamplePosition = _pendingSeekSample;
            _pendingSeekSample = -1;
            _fadeInTotal = _rampSamplesPerChannel;
            _fadeInRemaining = _rampSamplesPerChannel;

            // Releer el resto del buffer ya desde la nueva posición para no
            // perder frames que la UI espera recibir en este Read().
            var remainingFrames = framesRequested - framesRead;
            if (remainingFrames > 0)
            {
                var extraRead = _reader.Read(buffer, offset + read, remainingFrames * channels);
                ApplyFadeIn(buffer, offset + read, extraRead / channels, channels);
                read += extraRead;
                framesRead += extraRead / channels;
            }
        }
        else if (_fadeInRemaining > 0)
        {
            ApplyFadeIn(buffer, offset, framesRead, channels);
        }

        CurrentSamplePosition += framesRead;
        return read;
    }

    private void ApplyFadeOut(float[] buffer, int offset, int frames, int channels)
    {
        for (var f = 0; f < frames && _fadeOutRemaining > 0; f++, _fadeOutRemaining--)
        {
            var gain = _fadeOutRemaining / (float)_rampSamplesPerChannel;
            for (var ch = 0; ch < channels; ch++)
            {
                buffer[offset + f * channels + ch] *= gain;
            }
        }
    }

    private void ApplyFadeIn(float[] buffer, int offset, int frames, int channels)
    {
        for (var f = 0; f < frames && _fadeInRemaining > 0; f++, _fadeInRemaining--)
        {
            var gain = 1f - (_fadeInRemaining / (float)_fadeInTotal);
            for (var ch = 0; ch < channels; ch++)
            {
                buffer[offset + f * channels + ch] *= gain;
            }
        }
    }

    public void Dispose() => _reader.Dispose();
}
