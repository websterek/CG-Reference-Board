using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class ShiftPanPendingStateTests
{
    [Fact]
    public void BelowThreshold_StaysInPending()
    {
        var ctx = new FakeInteractionContext();
        var state = new ShiftPanPendingState(startScreen: new Point(100, 100));
        state.Enter(ctx);

        // Move only 3px — below 5px threshold
        ctx.InjectedScreenPosition = new Point(103, 100);
        var t = state.OnPointerMoved(null!, ctx);

        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void AboveThreshold_TransitionsToPanState()
    {
        var ctx = new FakeInteractionContext();
        var state = new ShiftPanPendingState(startScreen: new Point(100, 100));
        state.Enter(ctx);

        // Move 10px — above 5px threshold
        ctx.InjectedScreenPosition = new Point(110, 100);
        var t = state.OnPointerMoved(null!, ctx);

        Assert.Equal(TransitionKind.GoTo, t.Kind);
        Assert.IsType<PanState>(t.NextState);
    }

    [Fact]
    public void Release_Pops()
    {
        var ctx = new FakeInteractionContext();
        var state = new ShiftPanPendingState(startScreen: new Point(100, 100));
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
