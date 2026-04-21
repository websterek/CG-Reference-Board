using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers;

public sealed class LabelsLayer : IContentLayer, INotifyPropertyChanged
{
    public string Id => "labels";
    public string DisplayName => "Labels";
    public int ZIndex => LayerZOrder.Labels;
    public int CollisionLayerId => 2;

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

    public bool SupportsCellType(CellType type) => type == CellType.Label;

    public int GetCellZIndex(CellViewModel cell)
    {
        return cell.IsDragging
            ? LayerZOrder.LabelDragging
            : LayerZOrder.Labels;
    }

    public IEnumerable<CellViewModel> HitTest(Point canvasPosition)
    {
        foreach (var cell in Cells)
        {
            if (!cell.HasContent || !cell.IsInViewport) continue;

            var rect = new Rect(cell.CanvasX, cell.CanvasY, cell.PixelWidth, cell.PixelHeight);
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
