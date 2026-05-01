using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class MarqueePendingStateTests
{
    [Fact]
    public void Created_Successfully()
    {
        var state = new MarqueePendingState(startPoint: new Point(100, 100));
        Assert.NotNull(state);
    }

    [Fact]
    public void ReleaseWithoutAdditive_ClearsSelection()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        ctx.Selection.SelectCell(cell);

        var state = new MarqueePendingState(startPoint: new Point(100, 100), additive: false);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
        Assert.Equal(0, ctx.Selection.SelectionCount);
    }

    [Fact]
    public void ReleaseWithAdditive_KeepsSelection()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        ctx.Selection.SelectCell(cell);

        var state = new MarqueePendingState(startPoint: new Point(100, 100), additive: true);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
        Assert.Equal(1, ctx.Selection.SelectionCount);
    }

    [Fact]
    public void OnPointerMoved_Null_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new MarqueePendingState(new Point(100, 100));

        var t = state.OnPointerMoved(null!, ctx);

        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void OnPointerCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new MarqueePendingState(new Point(100, 100));

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnKeyDown_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new MarqueePendingState(new Point(100, 100));

        var t = state.OnKeyDown(null!, ctx);

        Assert.Equal(TransitionKind.Stay, t.Kind);
    }
}
