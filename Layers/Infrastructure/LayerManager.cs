using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers.Infrastructure;

public class LayerManager : INotifyPropertyChanged
{
    private readonly Dictionary<string, ILayer> _layersById = new();
    private readonly List<IContentLayer> _contentLayers = new();

    public IReadOnlyList<ILayer> Layers { get; }
    public IReadOnlyList<IContentLayer> ContentLayers => _contentLayers.AsReadOnly();

    public BackgroundLayer Background => (BackgroundLayer)_layersById["background"];
    public IContentLayer Backdrops => _contentLayers.First(l => l.Id == "backdrops");
    public IContentLayer Items => _contentLayers.First(l => l.Id == "items");
    public IContentLayer Labels => _contentLayers.First(l => l.Id == "labels");
    public AnnotationLayer Annotations => (AnnotationLayer)_layersById["annotations"];

    public ObservableCollection<CellViewModel> AllCells { get; } = new();

    private IContentLayer? _activeLayer;
    public IContentLayer? ActiveLayer
    {
        get => _activeLayer;
        set
        {
            if (_activeLayer == value) return;
            _activeLayer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveLayer)));
        }
    }

    public LayerManager()
    {
        var layers = new List<ILayer>
        {
            new BackgroundLayer(),
            new BackdropLayer(),
            new ItemsLayer(),
            new LabelsLayer(),
            new AnnotationLayer()
        };

        foreach (var layer in layers)
        {
            _layersById[layer.Id] = layer;
            if (layer is IContentLayer contentLayer)
                _contentLayers.Add(contentLayer);
        }

        Layers = layers.OrderBy(l => l.ZIndex).ToList().AsReadOnly();
        ActiveLayer = Items;
    }

    public IContentLayer? ResolveLayer(CellViewModel cell)
    {
        return _contentLayers.FirstOrDefault(l => l.SupportsCellType(cell.Type));
    }

    public IContentLayer? AddCell(CellViewModel cell)
    {
        var layer = ResolveLayer(cell);
        if (layer != null)
        {
            layer.Cells.Add(cell);
            if (!AllCells.Contains(cell))
                AllCells.Add(cell);
        }
        return layer;
    }

    public bool RemoveCell(CellViewModel cell)
    {
        var layer = _contentLayers.FirstOrDefault(l => l.Cells.Contains(cell));
        if (layer != null)
        {
            layer.Cells.Remove(cell);
            AllCells.Remove(cell);
            return true;
        }
        return false;
    }

    public CellViewModel? HitTestTopDown(Point canvasPosition)
    {
        foreach (var layer in _contentLayers.AsEnumerable().Reverse())
        {
            if (!layer.IsVisible || layer.IsLocked)
                continue;

            var hit = layer.HitTest(canvasPosition).FirstOrDefault();
            if (hit != null)
                return hit;
        }
        return null;
    }

    public void Clear()
    {
        foreach (var layer in _contentLayers)
            layer.Clear();
        Annotations.Clear();
        AllCells.Clear();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
