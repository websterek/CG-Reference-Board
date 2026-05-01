using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class ResizeStateTests
{
    [Fact]
    public void Resize_OnMove_UpdatesColSpanAndRowSpan()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1 };

        // Canvas position (320, 320) → newCols = round(320/160) = 2, newRows = round(320/160) = 2
        ctx.InjectedCanvasPosition = new Point(320, 320);

        var state = new ResizeState(cell, startColSpan: 1, startRowSpan: 1);
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(2, cell.ColSpan);
        Assert.Equal(2, cell.RowSpan);
    }

    [Fact]
    public void Resize_OnMove_ClampsToMinOne()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 100, CanvasY = 100, ColSpan = 2, RowSpan = 2 };

        // Canvas position (80, 80) → (80-100)/160 = negative → clamp to 1
        ctx.InjectedCanvasPosition = new Point(80, 80);

        var state = new ResizeState(cell, startColSpan: 2, startRowSpan: 2);
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(1, cell.ColSpan);
        Assert.Equal(1, cell.RowSpan);
    }

    [Fact]
    public void Resize_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1 };

        var state = new ResizeState(cell, startColSpan: 1, startRowSpan: 1);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void Resize_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1 };

        var state = new ResizeState(cell, startColSpan: 1, startRowSpan: 1);
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
