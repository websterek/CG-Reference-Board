using Avalonia;
using CGReferenceBoard.Services;
using Xunit;

namespace CGReferenceBoard.Tests.Services;

public class ViewportServiceTests
{
    [Fact]
    public void PanBy_MovesOffset()
    {
        var svc = new ViewportService();
        svc.PanBy(new Vector(10, 20));
        Assert.Equal(10, svc.OffsetX);
        Assert.Equal(20, svc.OffsetY);
    }

    [Fact]
    public void PanBy_Accumulates()
    {
        var svc = new ViewportService();
        svc.PanBy(new Vector(5, 3));
        svc.PanBy(new Vector(2, 1));
        Assert.Equal(7, svc.OffsetX);
        Assert.Equal(4, svc.OffsetY);
    }

    [Fact]
    public void ZoomAt_ZoomsAroundAnchor()
    {
        var svc = new ViewportService();
        svc.ZoomAt(new Point(100, 100), 2.0);
        Assert.Equal(2.0, svc.Zoom, precision: 6);
        Assert.Equal(-100, svc.OffsetX, precision: 6);
        Assert.Equal(-100, svc.OffsetY, precision: 6);
    }

    [Fact]
    public void ResetView_ResetsAll()
    {
        var svc = new ViewportService();
        svc.PanBy(new Vector(50, 50));
        svc.ZoomAt(new Point(0, 0), 2.0);
        svc.ResetView();
        Assert.Equal(1.0, svc.Zoom);
        Assert.Equal(0, svc.OffsetX);
        Assert.Equal(0, svc.OffsetY);
    }

    [Fact]
    public void FitToBoard_ClampsZoomAndCenters()
    {
        var svc = new ViewportService();
        svc.FitToBoard(new Rect(0, 0, 1000, 500), new Size(800, 400));
        // Min(800/1000, 400/500) * 0.9 = min(0.8, 0.8) * 0.9 = 0.72
        Assert.Equal(0.72, svc.Zoom, precision: 4);
    }

    [Fact]
    public void ZoomLevelText_ShowsPercentage()
    {
        var svc = new ViewportService();
        svc.Zoom = 1.5;
        Assert.Equal("150%", svc.ZoomLevelText);
    }

    [Fact]
    public void IsCanvasBackgroundVisible_FalseBelow25Percent()
    {
        var svc = new ViewportService();
        svc.Zoom = 0.24;
        Assert.False(svc.IsCanvasBackgroundVisible);
        svc.Zoom = 0.25;
        Assert.True(svc.IsCanvasBackgroundVisible);
    }

    [Fact]
    public void ZoomAt_ClampedToMaximum()
    {
        var svc = new ViewportService();
        svc.ZoomAt(new Point(0, 0), 1000.0);
        Assert.Equal(50.0, svc.Zoom, precision: 4);
    }

    [Fact]
    public void PropertyChanged_FiredOnZoom()
    {
        var svc = new ViewportService();
        var fired = new System.Collections.Generic.List<string>();
        svc.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");
        svc.Zoom = 2.0;
        Assert.Contains(nameof(svc.Zoom), fired);
        Assert.Contains(nameof(svc.ZoomInverseFactor), fired);
        Assert.Contains(nameof(svc.ZoomLevelText), fired);
    }
}
