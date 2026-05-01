using System;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is drawing a new annotation (Brush, Line, Arrow, Rectangle, Ellipse).
/// Text annotations are handled separately via the text editor overlay.
/// </summary>
public sealed class DrawAnnotationState : IInteractionState
{
    private readonly AnnotationViewModel _annotation;

    public DrawAnnotationState(AnnotationViewModel annotation)
    {
        _annotation = annotation;
    }

    public void Enter(IInteractionContext ctx)
    {
        ctx.Vm.Annotations.Add(_annotation);
    }

    public void Exit(IInteractionContext ctx)
    {
        _annotation.IsInDrawMode = false;
        ctx.FinishDrawAnnotation();
    }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        var pt = ctx.GetCanvasPosition(e);

        if (_annotation.Type == "Brush")
        {
            if (_annotation.Points.Count == 0
                || Math.Abs(pt.X - _annotation.Points[_annotation.Points.Count - 1].X) > 2
                || Math.Abs(pt.Y - _annotation.Points[_annotation.Points.Count - 1].Y) > 2)
            {
                _annotation.Points.Add(pt);
            }
        }
        else if (_annotation.Type != "Text")
        {
            if (_annotation.Points.Count < 2)
                _annotation.Points.Add(pt);
            else
                _annotation.Points[1] = pt;
        }

        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;
}
