using System.Collections.ObjectModel;
using System.ComponentModel;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers;

public sealed class AnnotationLayer : ILayer, INotifyPropertyChanged
{
    public string Id => "annotations";
    public string DisplayName => "Annotations";
    public int ZIndex => LayerZOrder.Annotations;

    public const int NoCollisionLayer = -1;

    public ObservableCollection<AnnotationViewModel> Items { get; } = new();

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                OnPropertyChanged(nameof(IsLocked));
            }
        }
    }

    public bool IsActive { get; set; }

    private double _opacity = 1.0;
    public double Opacity
    {
        get => _opacity;
        set
        {
            if (System.Math.Abs(_opacity - value) > 0.001)
            {
                _opacity = value;
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    public bool SupportsCellType(Models.CellType type) => false;

    public void Clear()
    {
        Items.Clear();
    }

    public void Dispose() => Clear();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
