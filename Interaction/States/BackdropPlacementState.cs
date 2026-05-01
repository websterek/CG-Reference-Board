using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is positioning a pending backdrop before placing it.
/// Left-click commits; right-click or Ctrl cancels.
/// </summary>
public sealed class BackdropPlacementState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var props = e.GetCurrentPoint(null).Properties;

        if (props.IsLeftButtonPressed)
        {
            if (ctx.TryPlacePendingBackdrop())
            {
                e.Handled = true;
                return StateTransition.Pop;
            }
            else
            {
                ctx.ShakeScreen();
                e.Handled = true;
                return StateTransition.Stay;
            }
        }

        if (props.IsRightButtonPressed || (e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            ctx.HidePlacementPreview();
            e.Handled = true;
            return StateTransition.Pop;
        }

        return StateTransition.Stay;
    }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        ctx.UpdatePlacementPreview(ctx.GetCanvasPosition(e));
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx)
    {
        ctx.HidePlacementPreview();
        return StateTransition.Pop;
    }

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx)
    {
        if (e?.Key == Key.Escape)
        {
            ctx.HidePlacementPreview();
            e.Handled = true;
            return StateTransition.Pop;
        }
        return StateTransition.Stay;
    }
}
