using Avalonia;
using CGReferenceBoard.Helpers;
using Xunit;

namespace CGReferenceBoard.Tests.Services;

public class GeometryTests
{
    [Fact]
    public void DistanceToSegment_PointOnSegment_ReturnsZero()
    {
        double dist = GeometryHelper.DistanceToSegment(
            new Point(5, 0), new Point(0, 0), new Point(10, 0));
        Assert.Equal(0.0, dist, precision: 6);
    }

    [Fact]
    public void DistanceToSegment_PointAboveSegment_ReturnsPerpendicularDistance()
    {
        double dist = GeometryHelper.DistanceToSegment(
            new Point(5, 3), new Point(0, 0), new Point(10, 0));
        Assert.Equal(3.0, dist, precision: 6);
    }

    [Fact]
    public void DistanceToSegment_PointBeyondEnd_ReturnsDistanceToEndpoint()
    {
        double dist = GeometryHelper.DistanceToSegment(
            new Point(15, 0), new Point(0, 0), new Point(10, 0));
        Assert.Equal(5.0, dist, precision: 6);
    }

    [Fact]
    public void DistanceToSegment_ZeroLengthSegment_ReturnsDistanceToPoint()
    {
        double dist = GeometryHelper.DistanceToSegment(
            new Point(3, 4), new Point(0, 0), new Point(0, 0));
        Assert.Equal(5.0, dist, precision: 6);
    }
}
