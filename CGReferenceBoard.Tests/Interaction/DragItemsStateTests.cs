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

    [Fact]
    public void DragItems_Enter_SetsDraggingOnAllCells()
    {
        var ctx = new FakeInteractionContext();
        var cell1 = new CellViewModel();
        var cell2 = new CellViewModel();

        var state = new DragItemsState(cell1, 0, 0, new[] { (cell1, 0.0, 0.0), (cell2, 160.0, 0.0) });
        state.Enter(ctx);

        Assert.True(cell1.IsDragging);
        Assert.True(cell2.IsDragging);
    }

    [Fact]
    public void DragItems_Exit_ClearsDraggingFlagsAndMarksUnsaved()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        var state = new DragItemsState(cell, 0, 0, new[] { (cell, 0.0, 0.0) });
        state.Enter(ctx);

        Assert.True(cell.IsDragging);
        state.Exit(ctx);

        Assert.False(cell.IsDragging);
        Assert.False(cell.IsDragInvalid);
        Assert.True(ctx.Vm.HasUnsavedChanges);
    }

    [Fact]
    public void DragItems_OnKeyDown_ReturnsStay()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        var state = new DragItemsState(cell, 0, 0, new[] { (cell, 0.0, 0.0) });
        state.Enter(ctx);

        var t = state.OnKeyDown(null!, ctx);

        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void GroupDrag_DeltaClampedToOneGridStep()
    {
        // If cursor jumps far (>GridSize delta), the move should be clamped to 1 grid step
        var ctx = new FakeInteractionContext();
        var cell1 = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        var cell2 = new CellViewModel { CanvasX = 160, CanvasY = 0 };

        // Primary starts at (0,0); inject canvas position at (960,0) — raw delta = 960
        // Clamped to GridSize = 160; cell1 → 160, cell2 → 320
        ctx.InjectedCanvasPosition = new Point(960, 0);

        var state = new DragItemsState(cell1, 0, 0, new[] { (cell1, 0.0, 0.0), (cell2, 160.0, 0.0) });
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(160, cell1.CanvasX, precision: 0);
        Assert.Equal(320, cell2.CanvasX, precision: 0);
    }

    [Fact]
    public void GroupDrag_NoMovementInDeadZone()
    {
        // dx and dy both < 0.1 → no cells should move
        var ctx = new FakeInteractionContext();
        var cell1 = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        var cell2 = new CellViewModel { CanvasX = 160, CanvasY = 0 };

        // Inject exactly the start position → targetX=0, targetY=0 → dx=0, dy=0
        ctx.InjectedCanvasPosition = new Point(0, 0);

        var state = new DragItemsState(cell1, 0, 0, new[] { (cell1, 0.0, 0.0), (cell2, 160.0, 0.0) });
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(0, cell1.CanvasX, precision: 0);
        Assert.Equal(160, cell2.CanvasX, precision: 0);
    }
}
