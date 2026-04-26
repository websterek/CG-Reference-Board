using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Skia;
using CGReferenceBoard;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformBoundsCalculatorTests
{
    static TransformBoundsCalculatorTests()
    {
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 })
            .SetupWithoutStarting();
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
    public void GetAnnotationBounds_UsesAbsoluteCachedBounds()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Rectangle" };
        annotation.Points.Add(new Point(5, 6));
        annotation.Points.Add(new Point(25, 36));
        annotation.UpdateBoundsCache();

        var bounds = TransformBoundsCalculator.GetAnnotationBounds(annotation);

        Assert.Equal(new Rect(3, 14, 44, 54), bounds);
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
            Thickness = 3
        };
        annotation.Points.Add(new Point(15, 25));
        annotation.Points.Add(new Point(500, 600));
        annotation.UpdateBoundsCache();

        double fontSize = Math.Max(12, annotation.Thickness * 4 + 10);
        var ft = new FormattedText(
            annotation.Text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, Arial"),
            fontSize,
            Brushes.White);

        var bounds = TransformBoundsCalculator.GetAnnotationBounds(annotation);

        Assert.Equal(new Rect(
            annotation.CanvasX + 4,
            annotation.CanvasY + 14,
            Math.Max(40, ft.Width + 20) + 22,
            Math.Max(20, ft.Height + 20) + 22), bounds);
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

        Assert.Equal(new Rect(160, 38, 402, 282), bounds);
    }

    [Fact]
    public void GetSelectionBounds_ReturnsNullForEmptySelection()
    {
        var bounds = TransformBoundsCalculator.GetSelectionBounds(Array.Empty<CellViewModel>(), Array.Empty<AnnotationViewModel>());

        Assert.Null(bounds);
    }
}
