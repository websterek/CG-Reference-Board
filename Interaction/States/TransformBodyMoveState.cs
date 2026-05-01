using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is dragging the transform body (move-selected-items gesture).
/// Delegates actual transform computation to the context, which has access to
/// TransformService, GridTransformService, etc.
/// </summary>
public sealed class TransformBodyMoveState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }

    public void Exit(IInteractionContext ctx)
    {
        ctx.FinishTransformMove();
    }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        ctx.UpdateTransformMove(ctx.GetCanvasPosition(e));
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
