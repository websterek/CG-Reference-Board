using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is dragging one or more cells across the canvas.
/// Handles single-cell and group drag (including backdrop expansion).
/// Snaps positions to grid on every move.
/// </summary>
public sealed class DragItemsState : IInteractionState
{
    private readonly CellViewModel _primary;
    private readonly double _dragOffsetX;
    private readonly double _dragOffsetY;
    private readonly IReadOnlyList<(CellViewModel Cell, double StartX, double StartY)> _groupStarts;

    public DragItemsState(
        CellViewModel primary,
        double dragOffsetX,
        double dragOffsetY,
        IEnumerable<(CellViewModel Cell, double StartX, double StartY)> groupStarts)
    {
        _primary = primary;
        _dragOffsetX = dragOffsetX;
        _dragOffsetY = dragOffsetY;
        _groupStarts = new List<(CellViewModel, double, double)>(groupStarts);
    }

    public void Enter(IInteractionContext ctx)
    {
        _primary.IsDragging = true;
        foreach (var (c, _, _) in _groupStarts)
            c.IsDragging = true;
    }

    public void Exit(IInteractionContext ctx)
    {
        foreach (var (c, _, _) in _groupStarts)
        {
            c.IsDragInvalid = false;
            c.IsDragging = false;
        }
        ctx.Vm.MarkUnsaved();
    }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        var canvasPos = ctx.GetCanvasPosition(e);

        double targetX = Math.Round((canvasPos.X - _dragOffsetX) / Constants.GridSize) * Constants.GridSize;
        double targetY = Math.Round((canvasPos.Y - _dragOffsetY) / Constants.GridSize) * Constants.GridSize;

        bool isGroup = _groupStarts.Count > 1;

        if (isGroup)
        {
            // Find primary start to compute delta
            var primaryStart = _groupStarts.FirstOrDefault(s => s.Cell == _primary);
            double dx = targetX - primaryStart.StartX;
            double dy = targetY - primaryStart.StartY;

            // Clamp delta to one grid step per move (smooth group movement)
            if (Math.Abs(dx) > Constants.GridSize)
                dx = Math.Sign(dx) * Constants.GridSize;
            if (Math.Abs(dy) > Constants.GridSize)
                dy = Math.Sign(dy) * Constants.GridSize;

            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
            {
                foreach (var (c, _, _) in _groupStarts)
                {
                    c.CanvasX += dx;
                    c.CanvasY += dy;
                }
            }
        }
        else
        {
            _primary.CanvasX = targetX;
            _primary.CanvasY = targetY;
        }

        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
