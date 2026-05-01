using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

public sealed class PanState : IInteractionState
{
    private Avalonia.Point _lastPoint;

    public PanState() { }
    public PanState(Avalonia.Point startPoint) { _lastPoint = startPoint; }

    public void SetStartPoint(Avalonia.Point pt) => _lastPoint = pt;

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { ctx.RequestViewportUpdate(); }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var pos = e.GetPosition(null);
        var screenDelta = pos - _lastPoint;
        _lastPoint = pos;
        // The canvas transform is Translate→Scale: screen = (canvas + tx) * zoom.
        // To keep the grab point fixed under the cursor, tx must change by screen_delta / zoom.
        double zoom = ctx.Viewport.Zoom;
        var canvasDelta = new Avalonia.Vector(screenDelta.X / zoom, screenDelta.Y / zoom);
        ctx.Viewport.PanBy(canvasDelta);
        ctx.NotifyZoomChanged();
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Pop;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Pop;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
