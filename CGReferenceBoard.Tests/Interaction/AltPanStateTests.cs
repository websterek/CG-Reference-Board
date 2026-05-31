using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.Services;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class AltPanStateTests
{
    [Fact]
    public void AltPan_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var t = state.OnPointerReleased(null!, ctx);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void AltPan_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var t = state.OnPointerCaptureLost(null!, ctx);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void AltPan_OnEscapeKey_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var result = state.OnKeyDown(new KeyEventArgs
        {
            Key = Key.Escape,
            KeyModifiers = KeyModifiers.None
        }, ctx);
        Assert.Equal(TransitionKind.Pop, result.Kind);
    }

    [Fact]
    public void AltPan_OnNonEscapeKey_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var result = state.OnKeyDown(new KeyEventArgs
        {
            Key = Key.A,
            KeyModifiers = KeyModifiers.None
        }, ctx);
        Assert.Equal(TransitionKind.Stay, result.Kind);
    }

    [Fact]
    public void AltPan_OnPressed_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var t = state.OnPointerPressed(null!, ctx);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void AltPan_OnPointerMoved_NullEvent_DoesNotCrash()
    {
        var vp = new ViewportService();
        var ctx = new FakeInteractionContext { ViewportOverride = vp };
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx);
        var result = state.OnPointerMoved(null!, ctx);
        Assert.Equal(StateTransition.Stay, result);
        Assert.Equal(0.0, vp.OffsetX, precision: 6);
        Assert.Equal(0.0, vp.OffsetY, precision: 6);
    }

    [Fact]
    public void AltPan_EnterAndExit_DoesNotThrow()
    {
        var ctx = new FakeInteractionContext();
        var state = new AltPanState(new Point(100, 100));
        state.Enter(ctx); // should not throw
        state.Exit(ctx); // should not throw (RequestViewportUpdate is called internally)
    }
}
