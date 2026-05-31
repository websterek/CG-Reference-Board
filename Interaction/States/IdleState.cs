using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Default quiescent state. Dispatches to other states based on button + modifiers.
/// </summary>
public sealed class IdleState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var props = e.GetCurrentPoint(null).Properties;

        // Backdrop placement takes priority over all other LMB handling
        if (ctx.IsShowingPlacementPreview && props.IsLeftButtonPressed)
            return StateTransition.GoTo(new BackdropPlacementState());

        // Backdrop: right-click or Ctrl cancels
        if (ctx.IsShowingPlacementPreview && (props.IsRightButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            ctx.HidePlacementPreview();
            e.Handled = true;
            return StateTransition.Stay;
        }

        // Alt+LMB: Always pan (overrides item hit-testing)
        if (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            ctx.SetPointerCapture(e.Pointer, true);
            e.Handled = true;
            return StateTransition.GoTo(new AltPanState(e.GetPosition(null)));
        }

        // Middle button: pan (default) or zoom (if Alt / Left also held)
        if (props.IsMiddleButtonPressed)
        {
            var screenPt = e.GetPosition(null);
            ctx.SetPointerCapture(e.Pointer, true);
            e.Handled = true;
            bool zoomMode = e.KeyModifiers.HasFlag(KeyModifiers.Alt) || props.IsLeftButtonPressed;
            return StateTransition.GoTo(new MiddleDragState(anchor: screenPt, screenY: screenPt.Y, zoomMode: zoomMode));
        }

        if (props.IsLeftButtonPressed)
        {
            var canvasPt = ctx.GetCanvasPosition(e);

            // Transform body move
            if (ctx.TryBeginTransformBodyMove(canvasPt))
            {
                ctx.SetPointerCapture(e.Pointer, true);
                e.Handled = true;
                return StateTransition.GoTo(new TransformBodyMoveState());
            }

            // Draw mode: eraser
            if (ctx.Vm.IsDrawMode && ctx.Vm.IsEraserMode)
                return StateTransition.GoTo(new EraseAnnotationState());

            // Draw mode: annotation move/select marquee
            if (ctx.Vm.IsDrawMode && ctx.Vm.IsMoveMode)
            {
                bool additive = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                return StateTransition.GoTo(new MarqueeSelectState(canvasPt, additive, annotationMode: true));
            }

            // Draw mode: draw annotation (non-Text)
            if (ctx.Vm.IsDrawMode && !ctx.Vm.IsEraserMode && !ctx.Vm.IsMoveMode)
            {
                var ann = ctx.BeginDrawAnnotation(canvasPt);
                if (ann != null)
                {
                    ctx.SetPointerCapture(e.Pointer, true);
                    e.Handled = true;
                    return StateTransition.GoTo(new DrawAnnotationState(ann));
                }
                // Text tool: fall through to let legacy overlay code handle it
                return StateTransition.Stay;
            }

            // Grid mode: cell marquee (via pending state for pan disambiguation)
            if (!ctx.Vm.IsDrawMode)
            {
                bool additive = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                return StateTransition.GoTo(new MarqueePendingState(e.GetPosition(null), additive));
            }
        }

        return StateTransition.Stay;
    }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
