using CommunityToolkit.Mvvm.ComponentModel;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.ViewModels;

public sealed class StemCategoryOptionViewModel : ObservableObject
{
    private bool _isSelected;

    public StemCategory Category { get; }
    public string DisplayName { get; }
    public bool IsAvailable { get; }
    public string? Tooltip { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public StemCategoryOptionViewModel(StemCategoryInfo info)
    {
        Category = info.Category;
        DisplayName = info.DisplayName;
        IsAvailable = info.IsAvailable;
        Tooltip = info.IsAvailable
            ? null
            : $"No disponible todavía: {info.UnavailableReason}";
    }
}
