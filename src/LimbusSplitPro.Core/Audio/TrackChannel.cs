using NAudio.Wave;

namespace LimbusSplitPro.Core.Audio;

/// <summary>
/// Una pista del mezclador: nombre, color/ícono (lo maneja la UI), y el
/// estado de mute/solo/volumen aplicado con una rampa corta (5 ms) cada vez
/// que cambia, para que nunca se oiga un clic (sección 16: "Los cambios
/// deben utilizar rampas para evitar clics").
/// </summary>
public sealed class TrackChannel : ISampleProvider
{
    private const int GainRampMilliseconds = 5;

    private readonly SeekableSampleTrack _source;
    private readonly int _rampSamples;
    private float _currentGain = 1f;
    private float _targetGain = 1f;
    private int _rampSamplesRemaining;

    public string Name => _source.StemName;
    public bool IsMuted { get; private set; }
    public bool IsSolo { get; private set; }
    public float Volume { get; private set; } = 1f; // 0.0 - 1.0 (o más, para permitir boost moderado)

    public WaveFormat WaveFormat => _source.WaveFormat;
    public long TotalSamples => _source.TotalSamples;
    public long CurrentSamplePosition => _source.CurrentSamplePosition;

    /// <summary>Función inyectada por el mezclador: true si hay algún solo activo en el grupo y este canal no es uno de ellos.</summary>
    public Func<TrackChannel, bool>? IsSilencedBySoloGroup { get; set; }

    public TrackChannel(SeekableSampleTrack source)
    {
        _source = source;
        _rampSamples = (int)(source.WaveFormat.SampleRate * GainRampMilliseconds / 1000.0);
    }

    public void SetMute(bool muted)
    {
        IsMuted = muted;
        RetargetGain();
    }

    public void SetSolo(bool solo)
    {
        IsSolo = solo;
        RetargetGain();
    }

    public void SetVolume(float volume)
    {
        Volume = Math.Max(0f, volume);
        RetargetGain();
    }

    /// <summary>Debe llamarse cuando cambia el estado global de solo del mezclador (otro canal entró/salió de solo).</summary>
    public void RefreshSoloState() => RetargetGain();

    private void RetargetGain()
    {
        var silencedBySolo = IsSilencedBySoloGroup?.Invoke(this) ?? false;
        _targetGain = (IsMuted || silencedBySolo) ? 0f : Volume;
        _rampSamplesRemaining = _rampSamples;
    }

    public void RequestSeek(long targetSamplePosition) => _source.RequestSeek(targetSamplePosition);

    public int Read(float[] buffer, int offset, int count)
    {
        var channels = WaveFormat.Channels;
        var read = _source.Read(buffer, offset, count);
        var frames = read / channels;

        for (var f = 0; f < frames; f++)
        {
            if (_rampSamplesRemaining > 0)
            {
                _currentGain += (_targetGain - _currentGain) / _rampSamplesRemaining;
                _rampSamplesRemaining--;
            }
            else
            {
                _currentGain = _targetGain;
            }

            for (var ch = 0; ch < channels; ch++)
            {
                buffer[offset + f * channels + ch] *= _currentGain;
            }
        }

        return read;
    }
}
