using Avalonia;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class AnnotationTransformServiceTests
{
    [Fact]
    public void Move_AddsDeltaToCanvasOffset()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(10, 10));
        var snapshots = TransformBoundsCalculator.CreateSnapshots(Array.Empty<CellViewModel>(), new[] { annotation });

        AnnotationTransformService.ApplyMove(snapshots, new Vector(5, -3));

        Assert.Equal(15, annotation.CanvasX);
        Assert.Equal(17, annotation.CanvasY);
    }

    [Fact]
    public void Resize_MapsAbsolutePointsIntoResizedBounds()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(100, 50));
        annotation.UpdateBoundsCache();
        var originalBounds = TransformBoundsCalculator.GetSelectionBounds(Array.Empty<CellViewModel>(), new[] { annotation })!.Value;
        var snapshots = TransformBoundsCalculator.CreateSnapshots(Array.Empty<CellViewModel>(), new[] { annotation });

        AnnotationTransformService.ApplyResize(snapshots, originalBounds, new Rect(10, 20, 200, 100));

        Assert.Equal(new Point(0, 0), annotation.Points[0]);
        Assert.Equal(29.35483870967742, annotation.CanvasX, 10);
        Assert.Equal(36.21621621621622, annotation.CanvasY, 10);
        Assert.Equal(161.29032258064515, annotation.Points[1].X, 10);
        Assert.Equal(67.56756756756756, annotation.Points[1].Y, 10);
    }
}
