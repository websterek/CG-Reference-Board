using System;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is performing middle+left drag-to-zoom gesture.
/// Translates vertical mouse movement into a multiplicative viewport zoom
/// anchored at the initial pointer position.
/// </summary>
public sealed class MiddleZoomState : IInteractionState
{
    private readonly Point _anchor;
    private readonly double _originY;
    private double _startY;
    private bool _active;

    public MiddleZoomState(Point anchor, double screenY)
    {
        _anchor = anchor;
        _originY = screenY;
        _startY = screenY;
        _active = false;
    }

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        // GetPosition in screen space (relative to window)
        var screenY = ctx.GetCanvasPosition(e).Y;

        if (!_active)
        {
            if (Math.Abs(screenY - _originY) < Constants.MiddleZoomDeadZone)
                return StateTransition.Stay;
            _active = true;
            // Use origin as startY so the first frame gets a real delta
        }

        double deltaY = _startY - screenY;
        double deltaLog = Math.Clamp(
            deltaY * Constants.MiddleZoomSensitivity,
            -Constants.MiddleZoomMaxDelta,
            Constants.MiddleZoomMaxDelta);
        double factor = Math.Exp(deltaLog);

        ctx.Viewport.ZoomAt(_anchor, factor);
        ctx.NotifyZoomChanged();

        _startY = screenY;
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
