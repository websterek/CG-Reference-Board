using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

// ═══════════════════════════════════════════════════════════════════════════════
// MarqueeSelectState
// ═══════════════════════════════════════════════════════════════════════════════

public class MarqueeSelectStateTests
{
    [Fact]
    public void Enter_AnnotationMode_CallsBeginAnnotationMarquee()
    {
        var ctx = new MarqueeTrackingCtx();
        new MarqueeSelectState(new Point(10, 20), additive: false, annotationMode: true).Enter(ctx);
        Assert.True(ctx.BeginAnnotationCalled);
        Assert.False(ctx.BeginCellCalled);
    }

    [Fact]
    public void Enter_CellMode_CallsBeginCellMarquee()
    {
        var ctx = new MarqueeTrackingCtx();
        new MarqueeSelectState(new Point(10, 20), additive: false, annotationMode: false).Enter(ctx);
        Assert.True(ctx.BeginCellCalled);
        Assert.False(ctx.BeginAnnotationCalled);
    }

    [Fact]
    public void OnPointerReleased_AnnotationMode_FinishesAndPops()
    {
        var ctx = new MarqueeTrackingCtx();
        var state = new MarqueeSelectState(new Point(0, 0), additive: false, annotationMode: true);
        state.Enter(ctx);
        var t = state.OnPointerReleased(null!, ctx);
        Assert.True(ctx.FinishAnnotationCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnPointerReleased_CellMode_FinishesAndPops()
    {
        var ctx = new MarqueeTrackingCtx();
        var state = new MarqueeSelectState(new Point(0, 0), additive: false, annotationMode: false);
        state.Enter(ctx);
        var t = state.OnPointerReleased(null!, ctx);
        Assert.True(ctx.FinishCellCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnPointerCaptureLost_AnnotationMode_FinishesAndPops()
    {
        var ctx = new MarqueeTrackingCtx();
        var state = new MarqueeSelectState(new Point(0, 0), false, annotationMode: true);
        state.Enter(ctx);
        var t = state.OnPointerCaptureLost(null!, ctx);
        Assert.True(ctx.FinishAnnotationCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnPointerCaptureLost_CellMode_FinishesAndPops()
    {
        var ctx = new MarqueeTrackingCtx();
        var state = new MarqueeSelectState(new Point(0, 0), false, annotationMode: false);
        state.Enter(ctx);
        var t = state.OnPointerCaptureLost(null!, ctx);
        Assert.True(ctx.FinishCellCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void Enter_AdditiveTrue_ForwardedToCellBegin()
    {
        var ctx = new AdditiveMarqueeCtx();
        new MarqueeSelectState(new Point(5, 10), additive: true, annotationMode: false).Enter(ctx);
        Assert.True(ctx.ReceivedAdditive);
        Assert.Equal(new Point(5, 10), ctx.ReceivedPoint);
    }

    private sealed class AdditiveMarqueeCtx : FakeInteractionContext
    {
        public bool ReceivedAdditive { get; private set; }
        public Point ReceivedPoint { get; private set; }
        public override void BeginCellMarqueeOverride(Point p, bool additive)
        {
            ReceivedAdditive = additive;
            ReceivedPoint = p;
        }
    }

    private sealed class MarqueeTrackingCtx : FakeInteractionContext
    {
        public bool BeginAnnotationCalled { get; private set; }
        public bool BeginCellCalled { get; private set; }
        public bool FinishAnnotationCalled { get; private set; }
        public bool FinishCellCalled { get; private set; }

        public override void BeginAnnotationMarqueeOverride(Point p, bool additive) => BeginAnnotationCalled = true;
        public override void BeginCellMarqueeOverride(Point p, bool additive) => BeginCellCalled = true;
        public override void FinishAnnotationMarqueeOverride() => FinishAnnotationCalled = true;
        public override void FinishCellMarqueeOverride() => FinishCellCalled = true;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// BackdropPlacementState
// ═══════════════════════════════════════════════════════════════════════════════

public class BackdropPlacementStateTests
{
    [Fact]
    public void OnKeyDown_Escape_HidesAndPops()
    {
        var ctx = new BackdropCtx(false);
        var state = new BackdropPlacementState();
        var t = state.OnKeyDown(null!, ctx); // null key → e?.Key is null, not Escape
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void OnPointerCaptureLost_HidesAndPops()
    {
        var ctx = new BackdropCtx(false);
        var state = new BackdropPlacementState();
        var t = state.OnPointerCaptureLost(null!, ctx);
        Assert.True(ctx.HidePreviewCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnPointerMoved_Null_ReturnsStay()
    {
        var ctx = new BackdropCtx(false);
        var state = new BackdropPlacementState();
        var t = state.OnPointerMoved(null!, ctx);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void OnPointerReleased_ReturnsStay()
    {
        var ctx = new BackdropCtx(false);
        var state = new BackdropPlacementState();
        var t = state.OnPointerReleased(null!, ctx);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    private sealed class BackdropCtx : FakeInteractionContext
    {
        private readonly bool _success;
        public bool TryPlaceCalled { get; private set; }
        public bool HidePreviewCalled { get; private set; }
        public bool ShakeCalled { get; private set; }
        public Point? LastPreviewPoint { get; private set; }

        public BackdropCtx(bool placementSuccess) => _success = placementSuccess;

        public override bool TryPlacePendingBackdropOverride() { TryPlaceCalled = true; return _success; }
        public override void HidePlacementPreviewOverride() => HidePreviewCalled = true;
        public override void ShakeScreenOverride() => ShakeCalled = true;
        public override void UpdatePlacementPreviewOverride(Point p) => LastPreviewPoint = p;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// AltDuplicateState
// ═══════════════════════════════════════════════════════════════════════════════

public class AltDuplicateStateTests
{
    [Fact]
    public void OnPointerCaptureLost_CancelsAndPops()
    {
        var ctx = new AltCtx();
        var t = new AltDuplicateState().OnPointerCaptureLost(null!, ctx);
        Assert.True(ctx.CancelCalled);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnKeyDown_Null_Stays()
    {
        var ctx = new AltCtx();
        var t = new AltDuplicateState().OnKeyDown(null!, ctx);
        Assert.False(ctx.CancelCalled);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void OnPointerReleased_Pops()
    {
        var ctx = new AltCtx();
        var t = new AltDuplicateState().OnPointerReleased(null!, ctx);
        Assert.Equal(TransitionKind.Pop, t.Kind);
    }

    [Fact]
    public void OnPointerMoved_ReturnsStay()
    {
        var ctx = new AltCtx();
        var t = new AltDuplicateState().OnPointerMoved(null!, ctx);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    [Fact]
    public void OnPointerPressed_ReturnsStay()
    {
        var ctx = new AltCtx();
        var t = new AltDuplicateState().OnPointerPressed(null!, ctx);
        Assert.Equal(TransitionKind.Stay, t.Kind);
    }

    private sealed class AltCtx : FakeInteractionContext
    {
        public bool CancelCalled { get; private set; }
        public override bool CancelAltDuplicateOverride() { CancelCalled = true; return true; }
    }
}
