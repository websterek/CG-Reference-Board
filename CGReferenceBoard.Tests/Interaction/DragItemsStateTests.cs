using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class DragItemsStateTests
{
    [Fact]
    public void DragItems_OnMove_UpdatesCellPosition()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 100, CanvasY = 100 };
        ctx.Selection.SelectCell(cell);

        // Simulate dragging: drag offset = (10, 10), cursor is at canvas (130, 140)
        // Expected: cell moves to (130 - 10, 140 - 10) = (120, 130)
        ctx.InjectedCanvasPosition = new Point(130, 140);

        var state = new DragItemsState(
            primary: cell,
            dragOffsetX: 10,
            dragOffsetY: 10,
            groupStarts: new[] { (cell, 100.0, 100.0) });
        state.Enter(ctx);

        var moved = StateTransition.Stay; // just needs an event arg placeholder
        state.OnPointerMoved(null!, ctx);

        // Cell should have moved to (cursor - dragOffset) snapped to grid
        // GridSize = 160, so round((130-10)/160)*160 = round(0.75)*160 = 1*160 = 160
        Assert.Equal(160, cell.CanvasX, precision: 0);
        Assert.Equal(160, cell.CanvasY, precision: 0);
    }

    [Fact]
    public void DragItems_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        var state = new DragItemsState(
            primary: cell,
            dragOffsetX: 0,
            dragOffsetY: 0,
            groupStarts: new[] { (cell, 0.0, 0.0) });
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void GroupDrag_OnMove_MovesAllCellsInLockstep()
    {
        var ctx = new FakeInteractionContext();
        var cell1 = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        var cell2 = new CellViewModel { CanvasX = 120, CanvasY = 0 };
        ctx.Selection.SelectCell(cell1);
        ctx.Selection.SelectCell(cell2, additive: true);

        // Drag cell1 with dragOffset (0,0), canvas position (160, 0)
        // cell1 should move to 160, cell2 should move to 320 (160 + 160 delta)
        ctx.InjectedCanvasPosition = new Point(160, 0);

        var state = new DragItemsState(
            primary: cell1,
            dragOffsetX: 0,
            dragOffsetY: 0,
            groupStarts: new[] { (cell1, 0.0, 0.0), (cell2, 120.0, 0.0) });
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(160, cell1.CanvasX, precision: 0);
        Assert.Equal(280, cell2.CanvasX, precision: 0);
    }

    [Fact]
    public void DragItems_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        var state = new DragItemsState(cell, 0, 0, new[] { (cell, 0.0, 0.0) });
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
