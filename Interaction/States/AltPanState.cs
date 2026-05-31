using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Entered when Alt+LMB is pressed. Always pans the canvas regardless of
/// whether the pointer is over a cell or annotation. Overrides item hit-testing.
/// </summary>
public sealed class AltPanState : IInteractionState
{
    private Point _lastPoint;

    public AltPanState(Point startPoint)
    {
        _lastPoint = startPoint;
    }

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { ctx.RequestViewportUpdate(); }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var pos = e.GetPosition(null);
        var screenDelta = pos - _lastPoint;
        _lastPoint = pos;
        double zoom = ctx.Viewport.Zoom;
        var canvasDelta = new Vector(screenDelta.X / zoom, screenDelta.Y / zoom);
        ctx.Viewport.PanBy(canvasDelta);
        ctx.NotifyZoomChanged();
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx)
    {
        if (e?.Key == Key.Escape)
        {
            e.Handled = true;
            return StateTransition.Pop;
        }
        return StateTransition.Stay;
    }
}
