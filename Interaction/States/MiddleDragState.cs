using System;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Entered when middle button is pressed. Dual-mode state:
/// - Pan sub-mode (default): Middle-drag pans the canvas
/// - Zoom sub-mode: Alt held or Left button also pressed -> vertical drag controls zoom
/// </summary>
public sealed class MiddleDragState : IInteractionState
{
    private readonly Point _zoomAnchor;
    private readonly double _originY;
    private double _zoomStartY;
    private Point _panLastPos;
    private bool _zoomMode;
    private bool _zoomActive;

    public MiddleDragState(Point anchor, double screenY, bool zoomMode = false)
    {
        _zoomAnchor = anchor;
        _originY = screenY;
        _zoomStartY = screenY;
        _panLastPos = anchor;
        _zoomMode = zoomMode;
        _zoomActive = false;
    }

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx)
    {
        if (e is not null)
        {
            var props = e.GetCurrentPoint(null).Properties;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) || props.IsLeftButtonPressed)
                _zoomMode = true;
        }
        return StateTransition.Stay;
    }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (_zoomMode)
        {
            var screenY = ctx.GetScreenPosition(e).Y;

            if (!_zoomActive)
            {
                if (Math.Abs(screenY - _originY) < Constants.MiddleZoomDeadZone)
                    return StateTransition.Stay;
                _zoomActive = true;
            }

            double deltaY = _zoomStartY - screenY;
            double deltaLog = Math.Clamp(
                deltaY * Constants.MiddleZoomSensitivity,
                -Constants.MiddleZoomMaxDelta,
                Constants.MiddleZoomMaxDelta);
            double factor = Math.Exp(deltaLog);

            ctx.Viewport.ZoomAt(_zoomAnchor, factor);
            ctx.NotifyZoomChanged();

            _zoomStartY = screenY;
        }
        else
        {
            var pos = e.GetPosition(null);
            var screenDelta = pos - _panLastPos;
            _panLastPos = pos;
            double zoom = ctx.Viewport.Zoom;
            var canvasDelta = new Vector(screenDelta.X / zoom, screenDelta.Y / zoom);
            ctx.Viewport.PanBy(canvasDelta);
            ctx.NotifyZoomChanged();
        }

        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx)
    {
        if (e?.Key == Key.LeftAlt || e?.Key == Key.RightAlt)
        {
            _zoomMode = true;
            return StateTransition.Stay;
        }
        return StateTransition.Stay;
    }
}
