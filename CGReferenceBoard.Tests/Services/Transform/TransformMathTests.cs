using Avalonia;
using CGReferenceBoard.Services.Transform;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(79, 0)]
    [InlineData(80, 0)]
    [InlineData(81, 160)]
    [InlineData(-81, -160)]
    public void SnapToGrid_RoundsToNearestGridLine(double value, double expected)
    {
        Assert.Equal(expected, TransformMath.SnapToGrid(value));
    }

    [Fact]
    public void SnapRectToGrid_SnapsEdgesBeforeDerivingSize()
    {
        var rect = new Rect(10, 20, 230, 170);

        var snapped = TransformMath.SnapRectToGrid(rect);

        Assert.Equal(new Rect(0, 0, 320, 160), snapped);
    }

    [Fact]
    public void SnapRectToGrid_EnforcesAtLeastOneGridCell()
    {
        var rect = new Rect(81, 81, 10, 10);

        var snapped = TransformMath.SnapRectToGrid(rect);

        Assert.Equal(new Rect(160, 160, 160, 160), snapped);
    }

    [Fact]
    public void ResizeBounds_BottomRightChangesWidthAndHeight()
    {
        var original = new Rect(100, 200, 300, 400);

        var resized = TransformMath.ResizeBounds(original, TransformHandle.BottomRight, new Vector(50, 60), minSize: 10);

        Assert.Equal(new Rect(100, 200, 350, 460), resized);
    }

    [Fact]
    public void ResizeBounds_TopLeftMovesOriginAndShrinksSize()
    {
        var original = new Rect(100, 200, 300, 400);

        var resized = TransformMath.ResizeBounds(original, TransformHandle.TopLeft, new Vector(40, 50), minSize: 10);

        Assert.Equal(new Rect(140, 250, 260, 350), resized);
    }

    [Fact]
    public void ResizeBounds_ClampsToMinimumSize()
    {
        var original = new Rect(100, 200, 300, 400);

        var resized = TransformMath.ResizeBounds(original, TransformHandle.Right, new Vector(-500, 0), minSize: 20);

        Assert.Equal(new Rect(100, 200, 20, 400), resized);
    }

    [Fact]
    public void GetScale_ReturnsIndependentAxisScale()
    {
        var original = new Rect(0, 0, 100, 200);
        var resized = new Rect(0, 0, 250, 100);

        var scale = TransformMath.GetScale(original, resized);

        Assert.Equal(new Vector(2.5, 0.5), scale);
    }
}
