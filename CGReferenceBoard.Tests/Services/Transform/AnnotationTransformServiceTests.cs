using Avalonia;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class AnnotationTransformServiceTests
{
    static AnnotationTransformServiceTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

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
        // originalBounds uses GetVisualBounds: visualPad=(4+3)/2+2=5.5
        // originalBounds = Rect(4.5, 14.5, 111, 61)
        // absolute(0,0) → mapped: x=10+5.5/111*200=19.9099..., y=20+5.5/61*100=29.0163...
        // absolute(100,50) → mapped: x=10+105.5/111*200=200.0909..., y=20+55.5/61*100=110.9836...
        Assert.Equal(19.909909909909908, annotation.CanvasX, 10);
        Assert.Equal(29.016393442622952, annotation.CanvasY, 10);
        Assert.Equal(180.18018018018017, annotation.Points[1].X, 10);
        Assert.Equal(81.96721311475411, annotation.Points[1].Y, 10);
    }

    [Fact]
    public void Resize_RepeatedCallsWithSameSnapshot_DoNotCompound()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(100, 50));
        annotation.UpdateBoundsCache();
        var originalBounds = TransformBoundsCalculator.GetSelectionBounds(Array.Empty<CellViewModel>(), new[] { annotation })!.Value;
        var resizedBounds = new Rect(10, 20, 200, 100);
        var snapshots = TransformBoundsCalculator.CreateSnapshots(Array.Empty<CellViewModel>(), new[] { annotation });

        AnnotationTransformService.ApplyResize(snapshots, originalBounds, resizedBounds);
        var firstCanvasX = annotation.CanvasX;
        var firstCanvasY = annotation.CanvasY;
        var firstPoint = annotation.Points[1];

        AnnotationTransformService.ApplyResize(snapshots, originalBounds, resizedBounds);

        Assert.Equal(firstCanvasX, annotation.CanvasX, 10);
        Assert.Equal(firstCanvasY, annotation.CanvasY, 10);
        Assert.Equal(firstPoint.X, annotation.Points[1].X, 10);
        Assert.Equal(firstPoint.Y, annotation.Points[1].Y, 10);
    }

    [Fact]
    public void Resize_TextAnnotation_IncreasesTextScale()
    {
        var annotation = new AnnotationViewModel
        {
            CanvasX = 100,
            CanvasY = 200,
            Type = "Text",
            Text = "Resize me",
            Thickness = 3
        };
        annotation.Points.Add(new Point(10, 20));
        annotation.UpdateBoundsCache();
        var originalBounds = new Rect(100, 200, 80, 40);
        var snapshots = new[] { TransformItemSnapshot.FromAnnotation(annotation, originalBounds) };

        AnnotationTransformService.ApplyResize(snapshots, originalBounds, new Rect(originalBounds.X, originalBounds.Y, originalBounds.Width * 2, originalBounds.Height * 2));

        Assert.Equal(2.0, annotation.TextScale, 10);
        Assert.Equal(new Point(10, 20), annotation.Points[0]);
    }

    [Fact]
    public void Resize_TextAnnotation_HorizontalOnlyShrink_UsesWidthScale()
    {
        var annotation = new AnnotationViewModel
        {
            CanvasX = 100,
            CanvasY = 200,
            Type = "Text",
            Text = "Resize me"
        };
        annotation.Points.Add(new Point(10, 20));
        annotation.UpdateBoundsCache();
        var originalBounds = new Rect(100, 200, 80, 40);
        var snapshots = new[] { TransformItemSnapshot.FromAnnotation(annotation, originalBounds) };

        AnnotationTransformService.ApplyResize(snapshots, originalBounds, new Rect(originalBounds.X, originalBounds.Y, originalBounds.Width * 0.5, originalBounds.Height));

        Assert.Equal(0.5, annotation.TextScale, 10);
    }
}
