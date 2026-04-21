using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Layers.Abstractions;

public interface IContentLayer : ILayer
{
    ObservableCollection<CellViewModel> Cells { get; }
    int CollisionLayerId { get; }
    int GetCellZIndex(CellViewModel cell);
    IEnumerable<CellViewModel> HitTest(Point canvasPosition);
    void Clear();
}
