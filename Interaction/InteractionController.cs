using Avalonia.Input;

namespace CGReferenceBoard.Interaction;

/// <summary>
/// Owns the current IInteractionState and routes pointer/keyboard events.
/// </summary>
public sealed class InteractionController : IInteractionController
{
    private IInteractionState _current;
    private readonly IInteractionContext _ctx;

    public IInteractionState CurrentState => _current;

    public InteractionController(IInteractionContext ctx, IInteractionState initialState)
    {
        _ctx = ctx;
        _current = initialState;
        _current.Enter(ctx);
    }

    private void Apply(StateTransition t)
    {
        switch (t.Kind)
        {
            case TransitionKind.GoTo when t.NextState is not null:
                _current.Exit(_ctx);
                _current = t.NextState;
                _current.Enter(_ctx);
                break;
            case TransitionKind.Pop:
                _current.Exit(_ctx);
                _current = new States.IdleState();
                _current.Enter(_ctx);
                break;
        }
    }

    public void OnPointerPressed(PointerPressedEventArgs e) =>
        Apply(_current.OnPointerPressed(e, _ctx));

    public void OnPointerMoved(PointerEventArgs e) =>
        Apply(_current.OnPointerMoved(e, _ctx));

    public void OnPointerReleased(PointerReleasedEventArgs e) =>
        Apply(_current.OnPointerReleased(e, _ctx));

    public void OnPointerCaptureLost(PointerCaptureLostEventArgs e) =>
        Apply(_current.OnPointerCaptureLost(e, _ctx));

    public void OnKeyDown(KeyEventArgs e) =>
        Apply(_current.OnKeyDown(e, _ctx));
}
