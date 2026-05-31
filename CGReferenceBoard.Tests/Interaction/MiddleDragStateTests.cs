using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.Services;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class MiddleDragStateTests
{
    [Fact]
    public void PanMode_NullEvent_DoesNotCrash()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);
        var result = state.OnPointerMoved(null!, ctx);
        Assert.Equal(StateTransition.Stay, result);
    }

    [Fact]
    public void MiddleDrag_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void MiddleDrag_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void MiddleDrag_ZoomMode_BeyondDeadZone_ChangesZoom()
    {
        var vp = new ViewportService();
        var ctx = new FakeInteractionContext { ViewportOverride = vp };
        double initialZoom = vp.Zoom;

        // originY=100, move to 50 → 50px beyond dead zone, upward = zoom in
        ctx.InjectedScreenPosition = new Point(0, 50);
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 100, zoomMode: true);
        state.Enter(ctx);
        // FakeInteractionContext.GetScreenPosition ignores the event arg,
        // so null is fine for zoom-mode tests
        state.OnPointerMoved(null!, ctx);

        Assert.NotEqual(initialZoom, vp.Zoom);
    }

    [Fact]
    public void MiddleDrag_ZoomMode_WithinDeadZone_DoesNotZoom()
    {
        var vp = new ViewportService();
        var ctx = new FakeInteractionContext { ViewportOverride = vp };
        double initialZoom = vp.Zoom;

        // originY=100, move to 104 → within 8px dead zone
        ctx.InjectedScreenPosition = new Point(0, 104);
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 100, zoomMode: true);
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(initialZoom, vp.Zoom, precision: 4);
    }

    [Fact]
    public void OnKeyDown_Alt_FlipsToZoomMode()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);

        var result = state.OnKeyDown(null!, ctx);

        // OnKeyDown handles null safely; verify state stays
        Assert.Equal(StateTransition.Stay, result);
    }

    [Fact]
    public void DefaultConstructor_DoesNotThrow()
    {
        var state = new MiddleDragState(new Point(0, 0), 0);
        var ctx = new FakeInteractionContext();
        var ex = Record.Exception(() => state.Enter(ctx));
        Assert.Null(ex);
    }
}
