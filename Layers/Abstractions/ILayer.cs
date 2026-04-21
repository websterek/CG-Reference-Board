using System;
using CGReferenceBoard.Models;

namespace CGReferenceBoard.Layers.Abstractions;

public interface ILayer : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    int ZIndex { get; }
    bool IsVisible { get; set; }
    bool IsLocked { get; set; }
    bool IsActive { get; set; }
    double Opacity { get; set; }
    bool SupportsCellType(CellType type);
}
