using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.Services;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class MiddleZoomStateTests
{
    [Fact]
    public void MiddleZoom_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleZoomState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void MiddleZoom_WithinDeadZone_DoesNotZoom()
    {
        var vp = new ViewportService();
        var ctx = new FakeInteractionContext { ViewportOverride = vp };
        double initialZoom = vp.Zoom;

        // originY=100, move to 104 → within 8px dead zone
        ctx.InjectedCanvasPosition = new Point(0, 104);
        var state = new MiddleZoomState(anchor: new Point(0, 0), screenY: 100);
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(initialZoom, vp.Zoom, precision: 4);
    }

    [Fact]
    public void MiddleZoom_BeyondDeadZone_ChangesZoom()
    {
        var vp = new ViewportService();
        var ctx = new FakeInteractionContext { ViewportOverride = vp };
        double initialZoom = vp.Zoom;

        // originY=100, move to 50 → 50px beyond dead zone, upward = zoom in
        ctx.InjectedCanvasPosition = new Point(0, 50);
        var state = new MiddleZoomState(anchor: new Point(0, 0), screenY: 100);
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.NotEqual(initialZoom, vp.Zoom);
    }

    [Fact]
    public void MiddleZoom_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleZoomState(new Point(0, 0), 0);
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
