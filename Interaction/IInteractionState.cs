using Avalonia.Input;

namespace CGReferenceBoard.Interaction;

/// <summary>
/// One gesture or interaction mode. Receives pointer/keyboard events from
/// <see cref="IInteractionController"/> and returns state transitions.
/// </summary>
public interface IInteractionState
{
    /// <summary>Called when the controller transitions into this state.</summary>
    void Enter(IInteractionContext ctx);

    /// <summary>Called when the controller transitions away from this state.</summary>
    void Exit(IInteractionContext ctx);

    StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx);
    StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx);
    StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx);
    StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx);
    StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx);
}
