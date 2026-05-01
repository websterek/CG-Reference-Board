using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

public class TransformBodyMoveStateTests
{
    [Fact]
    public void TransformBodyMove_OnRelease_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new TransformBodyMoveState();
        state.Enter(ctx);

        var t = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void TransformBodyMove_OnCaptureLost_ReturnsPop()
    {
        var ctx = new FakeInteractionContext();
        var state = new TransformBodyMoveState();
        state.Enter(ctx);

        var t = state.OnPointerCaptureLost(null!, ctx);

        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void TransformBodyMove_OnMove_CallsUpdateTransformMove()
    {
        var ctx = new FakeTransformContext();
        ctx.InjectedCanvasPosition = new Point(100, 200);

        var state = new TransformBodyMoveState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(new Point(100, 200), ctx.LastUpdatePoint);
    }
}

/// <summary>
/// Extended fake that tracks TransformMove calls.
/// </summary>
internal sealed class FakeTransformContext : FakeInteractionContext
{
    public Point? LastUpdatePoint { get; private set; }
    public bool BeginCalled { get; private set; }

    public override bool BeginTransformMove(Point pt) { BeginCalled = true; return true; }
    public override void UpdateTransformMove(Point pt) { LastUpdatePoint = pt; }
    public override void FinishTransformMove() { }
}
