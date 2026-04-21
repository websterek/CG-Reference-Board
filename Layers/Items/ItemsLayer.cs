using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers;

public sealed class ItemsLayer : IContentLayer, INotifyPropertyChanged
{
    public string Id => "items";
    public string DisplayName => "Items";
    public int ZIndex => LayerZOrder.Items;
    public int CollisionLayerId => 1;

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

    public bool SupportsCellType(CellType type)
        => type is CellType.Image or CellType.Video or CellType.Text;

    public int GetCellZIndex(CellViewModel cell)
    {
        return cell.IsDragging
            ? LayerZOrder.ItemDragging
            : LayerZOrder.Items;
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
