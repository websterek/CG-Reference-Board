using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformBoundsCalculatorTests
{
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
    public void GetAnnotationBounds_UsesAbsoluteCachedBounds()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Rectangle" };
        annotation.Points.Add(new Point(5, 6));
        annotation.Points.Add(new Point(25, 36));
        annotation.UpdateBoundsCache();

        var bounds = TransformBoundsCalculator.GetAnnotationBounds(annotation);

        Assert.Equal(new Rect(15, 26, 20, 30), bounds);
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

        Assert.Equal(new Rect(160, 50, 390, 270), bounds);
    }

    [Fact]
    public void GetSelectionBounds_ReturnsNullForEmptySelection()
    {
        var bounds = TransformBoundsCalculator.GetSelectionBounds(Array.Empty<CellViewModel>(), Array.Empty<AnnotationViewModel>());

        Assert.Null(bounds);
    }
}
