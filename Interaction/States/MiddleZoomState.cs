using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

public sealed class MiddleZoomState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Pop;
    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) => StateTransition.Pop;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
