using System;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Interaction.States;

/// <summary>
/// Active while the user is erasing annotations in draw/eraser mode.
/// On each pointer move, tests all annotations for intersection and removes those hit.
/// </summary>
public sealed class EraseAnnotationState : IInteractionState
{
    public void Enter(IInteractionContext ctx) { }
    public void Exit(IInteractionContext ctx) { }

    public StateTransition OnPointerPressed(PointerPressedEventArgs e, IInteractionContext ctx)
    {
        EraseAt(ctx.GetCanvasPosition(e), ctx);
        return StateTransition.Stay;
    }

    public StateTransition OnPointerMoved(PointerEventArgs e, IInteractionContext ctx)
    {
        EraseAt(ctx.GetCanvasPosition(e), ctx);
        return StateTransition.Stay;
    }

    public StateTransition OnPointerReleased(PointerReleasedEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnPointerCaptureLost(PointerCaptureLostEventArgs e, IInteractionContext ctx) =>
        StateTransition.Pop;

    public StateTransition OnKeyDown(KeyEventArgs e, IInteractionContext ctx) =>
        StateTransition.Stay;

    private static void EraseAt(Point pt, IInteractionContext ctx)
    {
        var vm = ctx.Vm;
        var toRemove = vm.Annotations.Where(ann =>
        {
            double threshold = Math.Max(15, ann.Thickness / 2 + 5);
            if (ann.Points.Count == 0)
                return false;
            if (ann.Type == "Rectangle" || ann.Type == "Ellipse" || ann.Type == "Text")
            {
                var pStart = new Point(ann.Points[0].X + ann.CanvasX, ann.Points[0].Y + ann.CanvasY);
                var pEnd = new Point(ann.Points[^1].X + ann.CanvasX, ann.Points[^1].Y + ann.CanvasY);
                double left = Math.Min(pStart.X, pEnd.X);
                double right = Math.Max(pStart.X, pEnd.X);
                double top = Math.Min(pStart.Y, pEnd.Y);
                double bottom = Math.Max(pStart.Y, pEnd.Y);

                if (ann.Type == "Text")
                {
                    var rendered = AnnotationBoundsHelper.GetRenderedBounds(ann);
                    left = rendered.X; top = rendered.Y;
                    right = rendered.Right; bottom = rendered.Bottom;
                }

                return pt.X >= left - threshold && pt.X <= right + threshold &&
                       pt.Y >= top - threshold && pt.Y <= bottom + threshold;
            }

            if (ann.Points.Count == 1)
            {
                var p0 = new Point(ann.Points[0].X + ann.CanvasX, ann.Points[0].Y + ann.CanvasY);
                return Math.Sqrt(Math.Pow(p0.X - pt.X, 2) + Math.Pow(p0.Y - pt.Y, 2)) < threshold;
            }

            for (int i = 0; i < ann.Points.Count - 1; i++)
            {
                var p1 = new Point(ann.Points[i].X + ann.CanvasX, ann.Points[i].Y + ann.CanvasY);
                var p2 = new Point(ann.Points[i + 1].X + ann.CanvasX, ann.Points[i + 1].Y + ann.CanvasY);
                if (GeometryHelper.DistanceToSegment(pt, p1, p2) < threshold)
                    return true;
            }
            return false;
        }).ToList();

        if (toRemove.Count == 0) return;

        foreach (var ann in toRemove)
        {
            vm.SelectionService.RemoveFromSelection(ann);
            vm.Annotations.Remove(ann);
        }
        vm.MarkUnsaved();
    }
}
