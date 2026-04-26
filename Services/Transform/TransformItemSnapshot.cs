using Avalonia;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Transform;

public sealed record TransformItemSnapshot(
    CellViewModel? Cell,
    AnnotationViewModel? Annotation,
    Rect Bounds,
    double CanvasX,
    double CanvasY,
    int ColSpan,
    int RowSpan)
{
    public static TransformItemSnapshot FromCell(CellViewModel cell, Rect bounds)
        => new(cell, null, bounds, cell.CanvasX, cell.CanvasY, cell.ColSpan, cell.RowSpan);

    public static TransformItemSnapshot FromAnnotation(AnnotationViewModel annotation, Rect bounds)
        => new(null, annotation, bounds, annotation.CanvasX, annotation.CanvasY, 0, 0);
}
