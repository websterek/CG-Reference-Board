using Avalonia;
using Avalonia.Input;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Rubber-band marquee selection state.
/// Handles both annotation-mode (SelectionMarquee) and grid-mode (CellSelectionMarquee)
/// selection drags, delegating all UI and hit-testing to IInteractionContext.
/// </summary>
public sealed class MarqueeSelectState : IInteractionState
{
    private readonly Point _startCanvasPt;
    private readonly bool _additive;
    private readonly bool _annotationMode; // true = annotation marquee, false = cell marquee

    /// <summary>Creates a marquee state for annotation mode.</summary>
    public MarqueeSelectState(Point startCanvasPt, bool additive, bool annotationMode)
    {
        _startCanvasPt = startCanvasPt;
        _additive = additive;
        _annotationMode = annotationMode;
    }

    /// <summary>Legacy no-arg ctor retained so existing call sites compile.</summary>
    public MarqueeSelectState() : this(default, false, false) { }

    /// <summary>Legacy additive-only ctor retained for existing call sites.</summary>
    public MarqueeSelectState(bool additive) : this(default, additive, false) { }

    public void Enter(IInteractionContext ctx)
    {
        if (_annotationMode)
            ctx.BeginAnnotationMarquee(_startCanvasPt, _additive);
        else
            ctx.BeginCellMarquee(_startCanvasPt, _additive);
    }

    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        if (e is null) return StateTransition.Stay;
        var pt = ctx.GetCanvasPosition(e);
        if (_annotationMode)
            ctx.UpdateAnnotationMarquee(pt);
        else
            ctx.UpdateCellMarquee(pt);
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx)
    {
        if (_annotationMode)
            ctx.FinishAnnotationMarquee();
        else
            ctx.FinishCellMarquee();
        ctx.SetPointerCapture(e?.Pointer, false);
        return StateTransition.Pop;
    }

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx)
    {
        if (_annotationMode)
            ctx.FinishAnnotationMarquee();
        else
            ctx.FinishCellMarquee();
        return StateTransition.Pop;
    }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) => StateTransition.Stay;
}
