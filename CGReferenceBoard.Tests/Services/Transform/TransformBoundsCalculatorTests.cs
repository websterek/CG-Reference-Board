using System;
using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformBoundsCalculatorTests
{
    static TransformBoundsCalculatorTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

    [Fact]
    public void GetCellBounds_UsesGridSpanSize()
    {
        var cell = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 320,
            ColSpan = 2,
            RowSpan = 3,
            Type = CellType.Image
        };

        var bounds = TransformBoundsCalculator.GetCellBounds(cell);

        Assert.Equal(new Rect(160, 320, 320, 480), bounds);
    }

    [Fact]
    public void GetCellBounds_BackdropUsesRenderedBackdropExtents()
    {
        var cell = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 320,
            ColSpan = 2,
            RowSpan = 3,
            Type = CellType.Backdrop
        };

        var bounds = TransformBoundsCalculator.GetCellBounds(cell);

        Assert.Equal(new Rect(cell.VisualX, cell.VisualY, cell.PixelWidth, cell.PixelHeight), bounds);
    }

    [Fact]
    public void GetAnnotationBounds_UsesAbsoluteCachedBounds()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Rectangle" };
        annotation.Points.Add(new Point(5, 6));
        annotation.Points.Add(new Point(25, 36));
        annotation.UpdateBoundsCache();

        var bounds = TransformBoundsCalculator.GetAnnotationBounds(annotation);

        // visualPad = (Thickness + OutlineExtraThickness) / 2 + ShadowOffset = (4+3)/2 + 2 = 5.5
        // bounds = Rect(CanvasX + localX - pad, CanvasY + localY - pad, localW + pad*2, localH + pad*2)
        //        = Rect(10 + 5 - 5.5, 20 + 6 - 5.5, 20 + 11, 30 + 11)
        //        = Rect(9.5, 20.5, 31, 41)
        Assert.Equal(new Rect(9.5, 20.5, 31, 41), bounds);
    }

    [Fact]
    public void GetAnnotationBounds_TextMatchesAnnotationShapeMeasurement()
    {
        var annotation = new AnnotationViewModel
        {
            CanvasX = 100,
            CanvasY = 200,
            Type = "Text",
            Text = "Transform box",
            Thickness = 3,
            TextScale = 1.5
        };
        annotation.Points.Add(new Point(15, 25));
        annotation.Points.Add(new Point(500, 600));
        annotation.UpdateBoundsCache();

        var bounds = TransformBoundsCalculator.GetAnnotationBounds(annotation);
        var unscaled = new AnnotationViewModel
        {
            CanvasX = 100,
            CanvasY = 200,
            Type = "Text",
            Text = "Transform box",
            Thickness = 3,
            TextScale = 1.0
        };
        unscaled.Points.Add(new Point(15, 25));
        unscaled.Points.Add(new Point(500, 600));
        unscaled.UpdateBoundsCache();
        var unscaledBounds = TransformBoundsCalculator.GetAnnotationBounds(unscaled);

        // visualPad = (3+3)/2 + 2 = 5.0
        // localBounds.X = 15, localBounds.Y = 25 (text overrides min point)
        // bounds.X = 100 + 15 - 5 = 110 = annotation.CanvasX + 10
        // bounds.Y = 200 + 25 - 5 = 220 = annotation.CanvasY + 20
        Assert.Equal(annotation.CanvasX + 10, bounds.X);
        Assert.Equal(annotation.CanvasY + 20, bounds.Y);
        Assert.True(bounds.Width > unscaledBounds.Width);
        Assert.True(bounds.Height > unscaledBounds.Height);
    }

    [Fact]
    public void GetSelectionBounds_UnionsCellsAndAnnotations()
    {
        var cell = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 160,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };
        var annotation = new AnnotationViewModel { CanvasX = 500, CanvasY = 50, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(50, 25));
        annotation.UpdateBoundsCache();

        var bounds = TransformBoundsCalculator.GetSelectionBounds(new[] { cell }, new[] { annotation });

        // Cell: Rect(160, 160, 160, 160)
        // Annotation: localBounds=(0,0,50,25), visualPad=(4+3)/2+2=5.5
        //   annBounds = Rect(500-5.5, 50-5.5, 61, 36) = Rect(494.5, 44.5, 61, 36)
        // Union: x=160, y=44.5, right=max(320,555.5)=555.5, bottom=max(320,80.5)=320
        //      = Rect(160, 44.5, 395.5, 275.5)
        Assert.Equal(new Rect(160, 44.5, 395.5, 275.5), bounds);
    }

    [Fact]
    public void GetSelectionBounds_ReturnsNullForEmptySelection()
    {
        var bounds = TransformBoundsCalculator.GetSelectionBounds(Array.Empty<CellViewModel>(), Array.Empty<AnnotationViewModel>());

        Assert.Null(bounds);
    }
}
