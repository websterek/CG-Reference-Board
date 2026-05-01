using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class EraseAnnotationStateTests
{
    [Fact]
    public void EraseAt_RemovesHitAnnotation()
    {
        var ctx = new FakeInteractionContext();

        // Brush annotation at (0,0) with one point at (50,50)
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);

        ctx.InjectedCanvasPosition = new Point(50, 50); // direct hit

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_KeepsUntouchedAnnotation()
    {
        var ctx = new FakeInteractionContext();

        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);

        ctx.InjectedCanvasPosition = new Point(500, 500); // far away

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Single(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAnnotation_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new EraseAnnotationState();
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }
}
