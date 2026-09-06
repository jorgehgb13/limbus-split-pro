using CommunityToolkit.Mvvm.ComponentModel;
using LimbusSplitPro.Core.Audio;

namespace LimbusSplitPro.App.ViewModels;

public sealed class TrackMixerViewModel : ObservableObject
{
    private readonly AudioMixerEngine _engine;
    private bool _isMuted;
    private bool _isSolo;
    private double _volumePercent = 100;

    public string Name { get; }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetProperty(ref _isMuted, value))
            {
                _engine.SetMute(Name, value);
            }
        }
    }

    public bool IsSolo
    {
        get => _isSolo;
        set
        {
            if (SetProperty(ref _isSolo, value))
            {
                _engine.SetSolo(Name, value);
            }
        }
    }

    /// <summary>0-150, mostrado como porcentaje; 100 = ganancia unitaria.</summary>
    public double VolumePercent
    {
        get => _volumePercent;
        set
        {
            if (SetProperty(ref _volumePercent, value))
            {
                _engine.SetVolume(Name, (float)(value / 100.0));
            }
        }
    }

    public TrackMixerViewModel(string name, AudioMixerEngine engine)
    {
        Name = name;
        _engine = engine;
    }
}
