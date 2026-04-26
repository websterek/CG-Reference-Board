using Avalonia;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Helpers;

public sealed class AnnotationBoundsHelperTests
{
    static AnnotationBoundsHelperTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

    [Fact]
    public void IntersectsRenderedBounds_UsesScaledTextExtents()
    {
        var annotation = new AnnotationViewModel
        {
            CanvasX = 300,
            CanvasY = 460,
            Type = "Text",
            Text = "Rendered bounds",
            TextScale = 2.5,
            Thickness = 3
        };
        annotation.Points.Add(new Point(0, 0));

        var target = new Rect(320, 480, 160, 160);

        Assert.True(AnnotationBoundsHelper.IntersectsRenderedBounds(annotation, target));
    }

    [Fact]
    public void GetRenderedBoundsUnion_UsesRenderedAnnotationExtents()
    {
        var text = new AnnotationViewModel
        {
            CanvasX = 100,
            CanvasY = 200,
            Type = "Text",
            Text = "Large text",
            TextScale = 2,
            Thickness = 3
        };
        text.Points.Add(new Point(0, 0));

        var brush = new AnnotationViewModel
        {
            CanvasX = 500,
            CanvasY = 520,
            Type = "Brush",
            Thickness = 4
        };
        brush.Points.Add(new Point(0, 0));
        brush.Points.Add(new Point(40, 30));
        brush.UpdateBoundsCache();

        var bounds = AnnotationBoundsHelper.GetRenderedBoundsUnion(new[] { text, brush });

        Assert.NotNull(bounds);
        Assert.True(bounds.Value.X < text.CanvasX + text.Points[0].X);
        Assert.True(bounds.Value.Y < text.CanvasY + text.Points[0].Y);
        Assert.True(bounds.Value.Right >= brush.CanvasX + brush.Points[1].X + brush.Thickness);
        Assert.True(bounds.Value.Bottom >= brush.CanvasY + brush.Points[1].Y + brush.Thickness);
    }

    [Fact]
    public void IntersectsRenderedGeometry_DoesNotSelectEmptyDiagonalBrushBoundsCorner()
    {
        var brush = new AnnotationViewModel
        {
            CanvasX = 0,
            CanvasY = 0,
            Type = "Brush",
            Thickness = 4
        };
        brush.Points.Add(new Point(0, 0));
        brush.Points.Add(new Point(160, 160));

        var emptyCorner = new Rect(120, 0, 40, 40);

        Assert.False(AnnotationBoundsHelper.IntersectsRenderedGeometry(brush, emptyCorner));
    }

    [Fact]
    public void IntersectsRenderedGeometry_SelectsBrushWhenStrokeCrossesTarget()
    {
        var brush = new AnnotationViewModel
        {
            CanvasX = 0,
            CanvasY = 0,
            Type = "Brush",
            Thickness = 4
        };
        brush.Points.Add(new Point(0, 0));
        brush.Points.Add(new Point(160, 160));

        var crossedRect = new Rect(70, 70, 20, 20);

        Assert.True(AnnotationBoundsHelper.IntersectsRenderedGeometry(brush, crossedRect));
    }
}
