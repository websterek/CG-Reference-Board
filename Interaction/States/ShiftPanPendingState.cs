using System;
using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Entered when Shift+Left is pressed. Waits for the pointer to move beyond a
/// 5-pixel threshold before committing to a pan. This allows Shift+double-click
/// to fire without accidentally starting a pan on a slow click.
/// </summary>
public sealed class ShiftPanPendingState : IInteractionState
{
    private const double PanThreshold = 5.0;
    private Point _startScreen;

    public ShiftPanPendingState() { }
    public ShiftPanPendingState(Point startScreen) { _startScreen = startScreen; }

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        var pos = ctx.GetScreenPosition(e);
        double dx = pos.X - _startScreen.X;
        double dy = pos.Y - _startScreen.Y;
        if (Math.Sqrt(dx * dx + dy * dy) > PanThreshold)
            return StateTransition.GoTo(new PanState(pos));
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
