using System;
using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Entered when LMB is pressed on empty canvas. Disambiguates:
/// - Move > threshold, no modifier → PanState
/// - Move > threshold, Ctrl held → MarqueeSelectState (additive)
/// - Release without move → click on empty (clear selection unless additive)
/// </summary>
public sealed class MarqueePendingState : IInteractionState
{
    private const double DragThreshold = 4.0;
    private readonly Point _startPoint;
    private readonly bool _additive;

    public MarqueePendingState() { }
    public MarqueePendingState(Point startPoint, bool additive = false)
    {
        _startPoint = startPoint;
        _additive = additive;
    }

    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var pos = e.GetPosition(null);
        var dx = pos.X - _startPoint.X;
        var dy = pos.Y - _startPoint.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > DragThreshold)
        {
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var canvasPt = ctx.GetCanvasPosition(e);

            if (ctx.Vm.IsDrawMode && ctx.Vm.IsMoveMode)
            {
                // Annotation mode: ctrl = additive marquee, otherwise also start marquee
                return StateTransition.GoTo(new MarqueeSelectState(canvasPt, additive: ctrl || _additive, annotationMode: true));
            }
            else if (!ctx.Vm.IsDrawMode)
            {
                if (ctrl || _additive)
                    return StateTransition.GoTo(new MarqueeSelectState(canvasPt, additive: true, annotationMode: false));
                else
                    return StateTransition.GoTo(new PanState(_startPoint));
            }
            else
            {
                // Draw mode (non-move): shouldn't reach here — pan fallback
                return StateTransition.GoTo(new PanState(_startPoint));
            }
        }
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx)
    {
        if (!_additive)
            ctx.Selection.ClearSelection();
        return StateTransition.Pop;
    }

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
