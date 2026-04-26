using System;
using System.Collections.Generic;
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

    public IReadOnlyList<Point> AnnotationPoints { get; }

    public double TextScale { get; }

    private TransformItemSnapshot(
        CellViewModel? cell,
        AnnotationViewModel? annotation,
        Rect bounds,
        double canvasX,
        double canvasY,
        int colSpan,
        int rowSpan,
        IReadOnlyList<Point>? annotationPoints,
        double textScale)
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
        AnnotationPoints = annotationPoints ?? Array.Empty<Point>();
        TextScale = textScale;
    }

    public static TransformItemSnapshot FromCell(CellViewModel cell, Rect bounds)
        => new(cell, null, bounds, cell.CanvasX, cell.CanvasY, cell.ColSpan, cell.RowSpan, null, 1.0);

    public static TransformItemSnapshot FromAnnotation(AnnotationViewModel annotation, Rect bounds)
        => new(null, annotation, bounds, annotation.CanvasX, annotation.CanvasY, 0, 0, new List<Point>(annotation.Points), annotation.TextScale);
}
