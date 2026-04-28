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

        if (props.IsMiddleButtonPressed)
            return StateTransition.GoTo(new MiddleZoomState());

        if (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return StateTransition.GoTo(new PanState());

        if (props.IsLeftButtonPressed)
            return StateTransition.GoTo(new MarqueePendingState());

        return StateTransition.Stay;
    }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
