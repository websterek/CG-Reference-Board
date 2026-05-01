using System;
using Avalonia.Input;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is resizing a cell via the resize thumb.
/// Updates ColSpan/RowSpan on every move; reverts on collision at release.
/// </summary>
public sealed class ResizeState : IInteractionState
{
    private readonly CellViewModel _cell;
    private readonly int _startColSpan;
    private readonly int _startRowSpan;

    public ResizeState(CellViewModel cell, int startColSpan, int startRowSpan)
    {
        _cell = cell;
        _startColSpan = startColSpan;
        _startRowSpan = startRowSpan;
    }

    public void Enter(IInteractionContext ctx) { }

    public void Exit(IInteractionContext ctx)
    {
        _cell.IsDragInvalid = false;
        ctx.Vm.RequestSave();
    }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        var pt = ctx.GetCanvasPosition(e);
        int newCols = Math.Max(1, (int)Math.Round((pt.X - _cell.CanvasX) / Constants.GridSize));
        int newRows = Math.Max(1, (int)Math.Round((pt.Y - _cell.CanvasY) / Constants.GridSize));

        _cell.ColSpan = newCols;
        _cell.RowSpan = newRows;

        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
