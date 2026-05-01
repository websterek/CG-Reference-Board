using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while an alt-duplicate drag is in flight.
/// The actual drag movement is still handled by the legacy code-behind;
/// this state owns only the cancel/commit lifecycle so the controller
/// can route Escape and pointer-release correctly.
/// </summary>
public sealed class AltDuplicateState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx)
        => StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
        => StateTransition.Stay; // handled by legacy code-behind

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx)
    {
        // Commit: legacy code-behind already finalized the drop; just pop.
        ctx.SetPointerCapture(e?.Pointer, false);
        return StateTransition.Pop;
    }

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx)
    {
        ctx.CancelAltDuplicate();
        return StateTransition.Pop;
    }

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx)
    {
        if (e?.Key == Key.Escape)
        {
            ctx.CancelAltDuplicate();
            e.Handled = true;
            return StateTransition.Pop;
        }
        return StateTransition.Stay;
    }
}
