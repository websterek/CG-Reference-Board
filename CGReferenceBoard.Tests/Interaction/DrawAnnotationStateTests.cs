using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class DrawAnnotationStateTests
{
    [Fact]
    public void DrawAnnotation_Enter_AddsAnnotationToVm()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush" };
        var state = new DrawAnnotationState(ann);

        state.Enter(ctx);

        Assert.Contains(ann, ctx.Vm.Annotations);
    }

    [Fact]
    public void Brush_OnMove_AddsPoints()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush" };
        ann.Points.Add(new Point(0, 0)); // initial point
        var state = new DrawAnnotationState(ann);
        state.Enter(ctx);

        // Move >2px from last point
        ctx.InjectedCanvasPosition = new Point(5, 5);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(2, ann.Points.Count);
    }

    [Fact]
    public void Brush_OnMove_SkipsPointIfTooClose()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush" };
        ann.Points.Add(new Point(0, 0));
        var state = new DrawAnnotationState(ann);
        state.Enter(ctx);

        // Move only 1px — below threshold
        ctx.InjectedCanvasPosition = new Point(1, 1);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(1, ann.Points.Count);
    }

    [Fact]
    public void Line_OnMove_UpdatesSecondPoint()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Line" };
        ann.Points.Add(new Point(0, 0)); // start point
        var state = new DrawAnnotationState(ann);
        state.Enter(ctx);

        ctx.InjectedCanvasPosition = new Point(100, 100);
        state.OnPointerMoved(null!, ctx);
        ctx.InjectedCanvasPosition = new Point(200, 200);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(2, ann.Points.Count);
        Assert.Equal(new Point(200, 200), ann.Points[1]);
    }

    [Fact]
    public void DrawAnnotation_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush" };
        var state = new DrawAnnotationState(ann);
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
