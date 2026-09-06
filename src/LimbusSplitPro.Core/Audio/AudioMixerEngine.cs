using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LimbusSplitPro.Core.Audio;

public enum TransportState
{
    Stopped,
    Playing,
    Paused,
}

/// <summary>
/// Mezclador multipista: un solo dispositivo de salida, un solo reloj, una
/// sola línea temporal (sección 14). Todas las pistas comparten
/// exactamente el mismo contador de posición porque todas leen el mismo
/// número de frames en cada Read() del grafo de mezcla — están garantizadas
/// a tener la misma duración/canales/frecuencia porque provienen del mismo
/// proceso de separación (ver docs/ARCHITECTURE.md).
/// </summary>
public sealed class AudioMixerEngine : IDisposable
{
    private readonly List<TrackChannel> _tracks = new();
    private MixingSampleProvider? _mixer;
    private WasapiOut? _output;
    private MMDevice? _currentDevice;

    public TransportState State { get; private set; } = TransportState.Stopped;
    public IReadOnlyList<TrackChannel> Tracks => _tracks;
    public event Action<string>? DeviceProblemOccurred;

    public WaveFormat? Format { get; private set; }

    public long TotalSamples => _tracks.Count == 0 ? 0 : _tracks.Max(t => t.TotalSamples);

    public TimeSpan CurrentPosition =>
        Format is null || _tracks.Count == 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(_tracks[0].CurrentSamplePosition / (double)Format.SampleRate);

    public TimeSpan TotalDuration =>
        Format is null ? TimeSpan.Zero : TimeSpan.FromSeconds(TotalSamples / (double)Format.SampleRate);

    /// <summary>
    /// Carga un conjunto de stems ya exportados por el motor (todos con el
    /// mismo formato — se valida explícitamente, no se asume).
    /// </summary>
    public void LoadTracks(IEnumerable<(string Name, string FilePath)> stems, MMDevice? outputDevice = null)
    {
        Reset();

        WaveFormat? commonFormat = null;
        foreach (var (name, path) in stems)
        {
            var seekable = new SeekableSampleTrack(name, path);
            if (commonFormat is null)
            {
                commonFormat = seekable.WaveFormat;
            }
            else if (seekable.WaveFormat.SampleRate != commonFormat.SampleRate ||
                     seekable.WaveFormat.Channels != commonFormat.Channels)
            {
                throw new InvalidOperationException(
                    $"La pista '{name}' tiene un formato distinto al resto " +
                    $"({seekable.WaveFormat.SampleRate}Hz/{seekable.WaveFormat.Channels}ch vs " +
                    $"{commonFormat.SampleRate}Hz/{commonFormat.Channels}ch). Esto no debería pasar " +
                    "si el motor exportó correctamente — es un bug a reportar, no algo para ignorar.");
            }

            var channel = new TrackChannel(seekable)
            {
                IsSilencedBySoloGroup = t => _tracks.Any(x => x.IsSolo) && !t.IsSolo,
            };
            _tracks.Add(channel);
        }

        Format = commonFormat;
        _mixer = new MixingSampleProvider(_tracks.Cast<ISampleProvider>())
        {
            ReadFully = true,
        };

        InitializeOutput(outputDevice);
    }

    private void InitializeOutput(MMDevice? outputDevice)
    {
        _currentDevice = outputDevice ?? new MMDeviceEnumerator()
            .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        // WASAPI compartido como modo estable por defecto (sección 14).
        _output = new WasapiOut(_currentDevice, AudioClientShareMode.Shared, true, 100);
        _output.PlaybackStopped += OnPlaybackStopped;
        _output.Init(_mixer);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            // Dispositivo desconectado, cambio de dispositivo predeterminado,
            // o el sistema entró en suspensión. No se deja el motor
            // bloqueado: se notifica y queda en Paused para poder
            // reintentar sobre el nuevo dispositivo predeterminado.
            State = TransportState.Paused;
            DeviceProblemOccurred?.Invoke(e.Exception.Message);
        }
    }

    public void SwitchOutputDevice(MMDevice newDevice)
    {
        var wasPlaying = State == TransportState.Playing;
        _output?.Stop();
        _output?.Dispose();
        InitializeOutput(newDevice);
        if (wasPlaying)
        {
            Play();
        }
    }

    public void Play()
    {
        if (_output is null)
        {
            throw new InvalidOperationException("No hay pistas cargadas.");
        }

        _output.Play();
        State = TransportState.Playing;
    }

    public void Pause()
    {
        _output?.Pause();
        State = TransportState.Paused;
    }

    /// <summary>Stop tiene semántica distinta de Pause: detiene Y regresa el cursor al inicio (sección 15).</summary>
    public void Stop()
    {
        _output?.Stop();
        SeekTo(TimeSpan.Zero);
        State = TransportState.Stopped;
    }

    public void SeekTo(TimeSpan position)
    {
        if (Format is null)
        {
            return;
        }

        var targetSample = (long)(position.TotalSeconds * Format.SampleRate);
        foreach (var track in _tracks)
        {
            track.RequestSeek(targetSample);
        }
    }

    public void SetMute(string trackName, bool muted) => GetTrack(trackName).SetMute(muted);
    public void SetSolo(string trackName, bool solo)
    {
        GetTrack(trackName).SetSolo(solo);
        foreach (var t in _tracks)
        {
            t.RefreshSoloState();
        }
    }
    public void SetVolume(string trackName, float volume) => GetTrack(trackName).SetVolume(volume);

    private TrackChannel GetTrack(string name) =>
        _tracks.FirstOrDefault(t => t.Name == name)
        ?? throw new ArgumentException($"No existe la pista '{name}'.");

    private void Reset()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _tracks.Clear();
        _mixer = null;
        Format = null;
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
    }
}
