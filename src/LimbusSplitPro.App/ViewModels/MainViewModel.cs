using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimbusSplitPro.Core.Audio;
using LimbusSplitPro.Core.Engine;
using LimbusSplitPro.Core.Models;
using Microsoft.Win32;

namespace LimbusSplitPro.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _positionTimer;
    private EngineClient? _engineClient;
    private CancellationTokenSource? _separationCts;

    public ObservableCollection<StemCategoryOptionViewModel> Categories { get; }
    public ObservableCollection<TrackMixerViewModel> Tracks { get; } = new();

    public AudioMixerEngine MixerEngine { get; } = new();

    private string? _inputFilePath;
    public string? InputFilePath
    {
        get => _inputFilePath;
        private set => SetProperty(ref _inputFilePath, value);
    }

    private string _inputFileSummary = "Ningún archivo seleccionado.";
    public string InputFileSummary
    {
        get => _inputFileSummary;
        private set => SetProperty(ref _inputFileSummary, value);
    }

    private string? _outputFolderPath;
    public string? OutputFolderPath
    {
        get => _outputFolderPath;
        private set => SetProperty(ref _outputFolderPath, value);
    }

    private string _devicePreference = "auto";
    public string DevicePreference
    {
        get => _devicePreference;
        set => SetProperty(ref _devicePreference, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (SetProperty(ref _isProcessing, value))
            {
                StartSeparationCommand.NotifyCanExecuteChanged();
                CancelSeparationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = "Listo.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    private bool _hasResults;
    public bool HasResults
    {
        get => _hasResults;
        private set => SetProperty(ref _hasResults, value);
    }

    private double _timelinePositionSeconds;
    public double TimelinePositionSeconds
    {
        get => _timelinePositionSeconds;
        set
        {
            if (SetProperty(ref _timelinePositionSeconds, value))
            {
                MixerEngine.SeekTo(TimeSpan.FromSeconds(value));
            }
        }
    }

    public double TimelineTotalSeconds => MixerEngine.TotalDuration.TotalSeconds;

    public IRelayCommand PickFileCommand { get; }
    public IRelayCommand PickFolderCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }
    public IAsyncRelayCommand StartSeparationCommand { get; }
    public IRelayCommand CancelSeparationCommand { get; }
    public IRelayCommand PlayCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand TogglePlayPauseCommand { get; }
    public IRelayCommand ExportMixCommand { get; }
    public IRelayCommand OpenOutputFolderCommand { get; }

    public MainViewModel()
    {
        Categories = new ObservableCollection<StemCategoryOptionViewModel>(
            StemCategoryCatalog.All
                .Where(c => c.Category != StemCategory.Other)
                .Select(c => new StemCategoryOptionViewModel(c)));

        // BUG CORREGIDO: CommunityToolkit.Mvvm.Input.RelayCommand, a
        // diferencia de MvvmLight, NO se re-evalúa solo cuando cambia una
        // propiedad — hay que avisarle explícitamente con
        // NotifyCanExecuteChanged(). Antes de este fix, marcar una casilla
        // de categoría nunca reactivaba el botón "Separar y exportar": se
        // quedaba deshabilitado (gris, sin responder a clics) aunque ya se
        // hubiera elegido archivo, carpeta y categoría, dando la impresión
        // de que "la app no hacía nada".
        foreach (var category in Categories)
        {
            category.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StemCategoryOptionViewModel.IsSelected))
                {
                    StartSeparationCommand.NotifyCanExecuteChanged();
                }
            };
        }

        PickFileCommand = new RelayCommand(PickFile);
        PickFolderCommand = new RelayCommand(PickFolder);
        SelectAllCommand = new RelayCommand(() => SetAll(true));
        SelectNoneCommand = new RelayCommand(() => SetAll(false));
        StartSeparationCommand = new AsyncRelayCommand(StartSeparationAsync, CanStartSeparation);
        CancelSeparationCommand = new RelayCommand(CancelSeparation, () => IsProcessing);
        PlayCommand = new RelayCommand(MixerEngine.Play);
        PauseCommand = new RelayCommand(MixerEngine.Pause);
        StopCommand = new RelayCommand(MixerEngine.Stop);
        TogglePlayPauseCommand = new RelayCommand(TogglePlayPause);
        ExportMixCommand = new RelayCommand(ExportMix, () => HasResults);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => OutputFolderPath is not null);

        MixerEngine.DeviceProblemOccurred += msg =>
            Application.Current.Dispatcher.Invoke(() => StatusMessage = $"Problema de audio: {msg}");

        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _positionTimer.Tick += (_, _) => OnPropertyChanged(nameof(TimelinePositionSecondsReadOnly));
        _positionTimer.Start();
    }

    /// <summary>Posición para mostrar en la UI sin disparar un seek al hacer binding de solo lectura.</summary>
    public double TimelinePositionSecondsReadOnly => MixerEngine.CurrentPosition.TotalSeconds;

    private void SetAll(bool selected)
    {
        foreach (var c in Categories.Where(c => c.IsAvailable))
        {
            c.IsSelected = selected;
        }
    }

    private void PickFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio (*.wav;*.mp3;*.flac;*.m4a)|*.wav;*.mp3;*.flac;*.m4a|Todos los archivos|*.*",
            Title = "Elegir canción",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        InputFilePath = dialog.FileName;
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(dialog.FileName);
            InputFileSummary =
                $"{Path.GetFileName(dialog.FileName)} · {reader.TotalTime:mm\\:ss} · " +
                $"{reader.WaveFormat.SampleRate} Hz · {reader.WaveFormat.Channels} canal(es)";
        }
        catch (Exception ex)
        {
            InputFileSummary = $"No se pudo leer el archivo: {ex.Message}";
            InputFilePath = null;
        }

        StartSeparationCommand.NotifyCanExecuteChanged();
    }

    private void PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Elegir carpeta de trabajo y exportación" };
        if (dialog.ShowDialog() == true)
        {
            OutputFolderPath = dialog.FolderName;
            StartSeparationCommand.NotifyCanExecuteChanged();
            OpenOutputFolderCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStartSeparation() =>
        !IsProcessing &&
        InputFilePath is not null &&
        OutputFolderPath is not null &&
        Categories.Any(c => c.IsSelected);

    private async Task StartSeparationAsync()
    {
        if (InputFilePath is null || OutputFolderPath is null)
        {
            return;
        }

        var selected = Categories.Where(c => c.IsSelected).Select(c => c.Category).ToHashSet();
        SeparationPlan plan;
        try
        {
            plan = SeparationPlanner.Plan(new SeparationRequest(selected));
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo planear la separación: {ex.Message}";
            return;
        }

        IsProcessing = true;
        HasResults = false;
        ProgressPercent = 0;
        StatusMessage = "Preparando…";
        _separationCts = new CancellationTokenSource();

        try
        {
            _engineClient = new EngineClient(App.EmbeddedPythonExePath, App.EngineScriptPath);
            _engineClient.EventReceived += evt =>
                Application.Current.Dispatcher.Invoke(() => HandleEngineEvent(evt));
            _engineClient.TechnicalLogReceived += line => Debug.WriteLine($"[engine] {line}");

            await _engineClient.StartAsync(_separationCts.Token);

            var modelInfo = App.Current is not null ? App.EmbeddedPythonExePath : string.Empty; // no-op, keeps analyzers quiet
            var command = new EngineCommand
            {
                Type = "separate",
                InputPath = InputFilePath,
                OutputDir = OutputFolderPath,
                ModelId = plan.ModelId,
                ModelPath = Path.Combine(App.UserDataRoot, "Models", plan.ModelId),
                NativeSourcesToExport = plan.NativeSourcesToKeepDirectly.ToList(),
                FoldIntoOther = plan.NativeSourcesToFoldIntoOther.ToList(),
                DevicePreference = DevicePreference,
            };

            await _engineClient.SendCommandAsync(command, _separationCts.Token);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al iniciar el motor: {ex.Message}";
            IsProcessing = false;
        }
    }

    private void HandleEngineEvent(EngineEvent evt)
    {
        switch (evt.Type)
        {
            case "stage":
                StatusMessage = evt.Stage switch
                {
                    "loading_model" => "Cargando modelo…",
                    "checking_memory" => "Comprobando memoria disponible…",
                    "separating" => "Separando…",
                    "writing_stems" => "Escribiendo pistas…",
                    _ => evt.Stage ?? StatusMessage,
                };
                break;
            case "progress":
                ProgressPercent = evt.Pct ?? ProgressPercent;
                break;
            case "error":
                IsProcessing = false;
                StatusMessage = TranslateError(evt.Code, evt.Message);
                break;
            case "done":
                IsProcessing = false;
                ProgressPercent = 100;
                StatusMessage = "Separación completa.";
                LoadResultsIntoMixer(evt.Stems ?? new List<EngineStemResult>());
                break;
        }
    }

    private static string TranslateError(string? code, string? message) => code switch
    {
        EngineErrorCode.ModelMissing => "El modelo elegido no está descargado todavía. Vuelve a intentar para descargarlo.",
        EngineErrorCode.HashMismatch => "El modelo descargado no coincide con lo esperado (hash inválido). Se volverá a descargar.",
        EngineErrorCode.UnsupportedFormat => "Ese formato de audio no es compatible.",
        EngineErrorCode.OutOfMemory => "No hay memoria suficiente para procesar este archivo completo. Prueba con CPU o un archivo más corto.",
        EngineErrorCode.GpuIncompatible => "La GPU detectada no es compatible; se usará CPU.",
        EngineErrorCode.PermissionDenied => "Permiso denegado al escribir en la carpeta elegida.",
        EngineErrorCode.FileLocked => "El archivo está siendo usado por otro programa.",
        EngineErrorCode.NetworkPathUnavailable => "La ruta de red no está disponible en este momento.",
        EngineErrorCode.AudioDeviceUnavailable => "El dispositivo de audio no está disponible en este momento.",
        EngineErrorCode.CancelledByUser => "Cancelado.",
        _ => message ?? "Ocurrió un error inesperado.",
    };

    private void LoadResultsIntoMixer(List<EngineStemResult> stems)
    {
        Tracks.Clear();
        MixerEngine.LoadTracks(stems.Select(s => (s.Name, s.Path)));
        foreach (var track in MixerEngine.Tracks)
        {
            Tracks.Add(new TrackMixerViewModel(track.Name, MixerEngine));
        }

        HasResults = Tracks.Count > 0;
        ExportMixCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(TimelineTotalSeconds));
    }

    private void CancelSeparation()
    {
        _separationCts?.Cancel();
        _ = _engineClient?.RequestCancelAsync(CancellationToken.None);
        StatusMessage = "Cancelando…";
    }

    private void TogglePlayPause()
    {
        if (MixerEngine.State == TransportState.Playing)
        {
            MixerEngine.Pause();
        }
        else
        {
            MixerEngine.Play();
        }
    }

    private void ExportMix()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "WAV (*.wav)|*.wav",
            FileName = "mezcla.wav",
            InitialDirectory = OutputFolderPath,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = MixExporter.Export(MixerEngine.Tracks.ToList(), new MixExportOptions(dialog.FileName));
            StatusMessage = result.LimiterWasApplied
                ? $"Mezcla exportada con protección de clipping ({result.LimiterGainReductionDb:F1} dB de reducción)."
                : "Mezcla exportada.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo exportar la mezcla: {ex.Message}";
        }
    }

    private void OpenOutputFolder()
    {
        if (OutputFolderPath is not null && Directory.Exists(OutputFolderPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{OutputFolderPath}\"") { UseShellExecute = true });
        }
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        MixerEngine.Dispose();
        _engineClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
