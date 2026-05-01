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

    [Fact]
    public void EraseAnnotation_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new EraseAnnotationState();
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void EraseAt_OnPointerPressed_AlsoErases()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);

        ctx.InjectedCanvasPosition = new Point(50, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerPressed(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_SegmentHit_RemovesBrushAnnotation()
    {
        var ctx = new FakeInteractionContext();
        // Brush with two points: (0,0) to (100,0)
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        ann.Points.Add(new Point(0, 0));
        ann.Points.Add(new Point(100, 0));
        ctx.Vm.Annotations.Add(ann);

        // Eraser at (50, 5) — within threshold of the segment
        ctx.InjectedCanvasPosition = new Point(50, 5);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_RectangleAnnotation_RemovedWhenInsideBounds()
    {
        var ctx = new FakeInteractionContext();
        // Rectangle from (0,0) to (100,100) in canvas space
        var ann = new AnnotationViewModel { Type = "Rectangle", Thickness = 2 };
        ann.Points.Add(new Point(0, 0));
        ann.Points.Add(new Point(100, 100));
        ctx.Vm.Annotations.Add(ann);

        // Eraser at center
        ctx.InjectedCanvasPosition = new Point(50, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_WithCanvasOffset_UsesOffsetCoordinates()
    {
        var ctx = new FakeInteractionContext();
        // Annotation at CanvasX=200, point at (50,50) → effective position (250,50)
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10, CanvasX = 200 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);

        // Hit at world coords (250, 50) — should erase
        ctx.InjectedCanvasPosition = new Point(250, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_WithCanvasOffset_MissesIfNotTranslated()
    {
        var ctx = new FakeInteractionContext();
        // Annotation at CanvasX=200, point at (50,50) → effective position (250,50)
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10, CanvasX = 200 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);

        // Eraser at local coords (50,50) — miss (200 units away)
        ctx.InjectedCanvasPosition = new Point(50, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Single(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_ZeroPointAnnotation_NotRemoved()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        // no points added
        ctx.Vm.Annotations.Add(ann);

        ctx.InjectedCanvasPosition = new Point(0, 0);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Single(ctx.Vm.Annotations);
    }

    [Fact]
    public void EraseAt_MarkUnsaved_CalledOnHit()
    {
        var ctx = new FakeInteractionContext();
        var ann = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
        ann.Points.Add(new Point(50, 50));
        ctx.Vm.Annotations.Add(ann);
        ctx.InjectedCanvasPosition = new Point(50, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.True(ctx.Vm.HasUnsavedChanges);
    }

    [Fact]
    public void EraseAt_MultipleAnnotations_RemovesAllHit()
    {
        var ctx = new FakeInteractionContext();
        for (int i = 0; i < 3; i++)
        {
            var a = new AnnotationViewModel { Type = "Brush", Thickness = 10 };
            a.Points.Add(new Point(50, 50));
            ctx.Vm.Annotations.Add(a);
        }
        ctx.InjectedCanvasPosition = new Point(50, 50);

        var state = new EraseAnnotationState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Empty(ctx.Vm.Annotations);
    }
}
