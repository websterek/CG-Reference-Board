using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers;

public sealed class BackdropLayer : IContentLayer, INotifyPropertyChanged
{
    public string Id => "backdrops";
    public string DisplayName => "Backdrops";
    public int ZIndex => LayerZOrder.Backdrops;
    public int CollisionLayerId => 0;

    public ObservableCollection<CellViewModel> Cells { get; } = new();

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
            if (Math.Abs(_opacity - value) > 0.001)
            {
                _opacity = value;
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    public bool SupportsCellType(CellType type) => type == CellType.Backdrop;

    public int GetCellZIndex(CellViewModel cell)
    {
        return cell.IsDragging
            ? LayerZOrder.BackdropDragging
            : LayerZOrder.Backdrops;
    }

    public IEnumerable<CellViewModel> HitTest(Point canvasPosition)
    {
        foreach (var cell in Cells)
        {
            if (!cell.HasContent) continue;

            var rect = new Rect(cell.VisualX, cell.VisualY, cell.PixelWidth, cell.PixelHeight);
            if (rect.Contains(canvasPosition))
                yield return cell;
        }
    }

    public void Clear()
    {
        foreach (var cell in Cells)
            cell.Dispose();
        Cells.Clear();
    }

    public void Dispose() => Clear();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
