using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
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
    public void OnPointerPressed_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);
        var result = state.OnPointerPressed(null!, ctx);
        Assert.Equal(TransitionKind.Stay, result.Kind);
    }

    [Fact]
    public void OnKeyDown_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new MiddleDragState(anchor: new Point(0, 0), screenY: 0);
        state.Enter(ctx);
        var result = state.OnKeyDown(null!, ctx);
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

    [Fact]
    public void EnterAndExit_DoesNotThrow()
    {
        var state = new MiddleDragState(new Point(0, 0), 0);
        var ctx = new FakeInteractionContext();
        state.Enter(ctx);
        state.Exit(ctx);
    }
}
