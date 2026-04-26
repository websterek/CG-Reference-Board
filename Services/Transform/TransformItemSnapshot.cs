using System;
using Avalonia;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Transform;

public sealed class TransformItemSnapshot
{
    public CellViewModel? Cell { get; }

    public AnnotationViewModel? Annotation { get; }

    public Rect Bounds { get; }

    public double CanvasX { get; }

    public double CanvasY { get; }

    public int ColSpan { get; }

    public int RowSpan { get; }

    private TransformItemSnapshot(
        CellViewModel? cell,
        AnnotationViewModel? annotation,
        Rect bounds,
        double canvasX,
        double canvasY,
        int colSpan,
        int rowSpan)
    {
        if ((cell is null) == (annotation is null))
        {
            throw new ArgumentException("Exactly one transform item source must be provided.");
        }

        Cell = cell;
        Annotation = annotation;
        Bounds = bounds;
        CanvasX = canvasX;
        CanvasY = canvasY;
        ColSpan = colSpan;
        RowSpan = rowSpan;
    }

    public static TransformItemSnapshot FromCell(CellViewModel cell, Rect bounds)
        => new(cell, null, bounds, cell.CanvasX, cell.CanvasY, cell.ColSpan, cell.RowSpan);

    public static TransformItemSnapshot FromAnnotation(AnnotationViewModel annotation, Rect bounds)
        => new(null, annotation, bounds, annotation.CanvasX, annotation.CanvasY, 0, 0);
}
