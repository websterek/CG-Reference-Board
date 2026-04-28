namespace CGReferenceBoard.Interaction;

/// <summary>
/// Describes what the controller should do after an event is processed.
/// </summary>
public readonly struct StateTransition
{
    public static readonly StateTransition Stay = new(TransitionKind.Stay, null);

    public static StateTransition GoTo(IInteractionState next) =>
        new(TransitionKind.GoTo, next);

    public static readonly StateTransition Pop = new(TransitionKind.Pop, null);

    public TransitionKind Kind { get; }
    public IInteractionState? NextState { get; }

    private StateTransition(TransitionKind kind, IInteractionState? next)
    {
        Kind = kind;
        NextState = next;
    }
}

public enum TransitionKind { Stay, GoTo, Pop }
