using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Models;
using CGReferenceBoard.Layers.Background;

namespace CGReferenceBoard.Layers;

public sealed class BackgroundLayer : ILayer, INotifyPropertyChanged
{
    public string Id => "background";
    public string DisplayName => "Canvas Background";
    public int ZIndex => LayerZOrder.Background;

    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = true;
    public bool IsActive { get; set; }
    public double Opacity { get; set; } = 1.0;

    public bool SupportsCellType(CellType type) => false;

    private readonly Dictionary<string, IBackgroundRenderer> _renderers = new()
    {
        ["Dots"] = new DotBackgroundRenderer(),
        ["Grid"] = new GridBackgroundRenderer(),
        ["None"] = new NoneBackgroundRenderer()
    };

    private string _mode = "Dots";
    public string Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(CurrentBrush));
        }
    }

    public IBrush? CurrentBrush
        => _renderers.TryGetValue(_mode, out var renderer) ? renderer.CreateBrush() : null;

    public void Dispose() { }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
