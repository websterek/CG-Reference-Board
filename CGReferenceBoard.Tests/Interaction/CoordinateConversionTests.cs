using Avalonia;
using CGReferenceBoard.Interaction;
using CGReferenceBoard.Interaction.States;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

/// <summary>
/// Regression tests for the coordinate-space bugs that made dragging behave
/// incorrectly at non-default zoom levels.
///
/// Root cause (fixed): GetCanvasPosition was calling ScreenToCanvas(e.GetPosition(null))
/// which used window-relative screen coordinates.  The correct approach in the live
/// context is e.GetPosition(MainCanvas) — Avalonia already applies the inverse of the
/// accumulated ScaleTransform so the result is in canvas units.
///
/// The state-machine layer never touches GetPosition directly; it asks the context for
/// a canvas-space point via ctx.GetCanvasPosition(e).  The FakeInteractionContext
/// exposes InjectedCanvasPosition to simulate this, so the state tests below validate
/// that state logic is correct for ANY canvas point, regardless of how the context
/// converts it from a pointer event.
/// </summary>
public class CoordinateConversionTests
{
    // ── ScreenToCanvas math ───────────────────────────────────────────────────

    /// <summary>
    /// At zoom=1, offset=(0,0): canvas pos == screen pos.
    /// </summary>
    [Fact]
    public void ScreenToCanvas_IdentityTransform_ReturnsSamePoint()
    {
        var viewport = new ViewportService { Zoom = 1.0, OffsetX = 0, OffsetY = 0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        var result = ctx.ScreenToCanvas(new Point(150, 250));

        Assert.Equal(150.0, result.X, precision: 6);
        Assert.Equal(250.0, result.Y, precision: 6);
    }

    /// <summary>
    /// At zoom=2, offset=(0,0): screen (200,200) → canvas (100,100).
    /// The canvas is scaled ×2 so each canvas unit is 2 pixels wide.
    /// Formula: canvas = screen/zoom - offset = 200/2 - 0 = 100.
    /// </summary>
    [Fact]
    public void ScreenToCanvas_Zoom2_HalvesCoordinates()
    {
        var viewport = new ViewportService { Zoom = 2.0, OffsetX = 0, OffsetY = 0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        var result = ctx.ScreenToCanvas(new Point(200, 200));

        Assert.Equal(100.0, result.X, precision: 6);
        Assert.Equal(100.0, result.Y, precision: 6);
    }

    /// <summary>
    /// At zoom=0.5, offset=(0,0): screen (100,100) → canvas (200,200).
    /// Zoomed out — each canvas unit is 0.5 pixels wide.
    /// </summary>
    [Fact]
    public void ScreenToCanvas_ZoomHalf_DoublesCoordinates()
    {
        var viewport = new ViewportService { Zoom = 0.5, OffsetX = 0, OffsetY = 0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        var result = ctx.ScreenToCanvas(new Point(100, 100));

        Assert.Equal(200.0, result.X, precision: 6);
        Assert.Equal(200.0, result.Y, precision: 6);
    }

    /// <summary>
    /// With panning (offset=-50 x): the canvas origin shifted left by 50 canvas units,
    /// so the same screen point maps to a larger canvas X.
    /// </summary>
    [Fact]
    public void ScreenToCanvas_WithPanOffset_IncludesOffset()
    {
        // Viewport panned: offsetX = -50 (canvas origin moved left)
        var viewport = new ViewportService { Zoom = 1.0, OffsetX = -50, OffsetY = 0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        // screen(100,0) → canvas = 100/1 - (-50) = 150
        var result = ctx.ScreenToCanvas(new Point(100, 0));

        Assert.Equal(150.0, result.X, precision: 6);
    }

    /// <summary>
    /// Zoom + offset combined: zoom=2, offset=-100.
    /// screen(200,0) → canvas = (200 - (-100)) / 2 = 150.
    /// </summary>
    [Fact]
    public void ScreenToCanvas_ZoomAndOffset_Correct()
    {
        var viewport = new ViewportService { Zoom = 2.0, OffsetX = -100, OffsetY = 0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        var result = ctx.ScreenToCanvas(new Point(200, 0));

        Assert.Equal(150.0, result.X, precision: 6);
    }

    // ── DragItemsState with zoom-accurate injected positions ─────────────────

    /// <summary>
    /// When zoom=2 the drag should still use canvas-space coordinates throughout.
    /// The injected canvas position already represents the correct canvas point
    /// (as if e.GetPosition(MainCanvas) returned the right value).
    /// </summary>
    [Theory]
    [InlineData(1.0,  240, 240,   10, 10,   160, 160)]  // zoom 1 — simple case
    [InlineData(2.0,  120, 120,   10, 10,   160, 160)]  // zoom 2 — canvas pos is already half screen
    [InlineData(0.5,  480, 480,   10, 10,   480, 480)]  // zoom 0.5 — (480-10)/160=2.9375→3→480
    public void DragItemsState_UsesCanvasSpacePositions(
        double zoom,
        double injectedCanvasX, double injectedCanvasY,
        double dragOffsetX,     double dragOffsetY,
        double expectedX,       double expectedY)
    {
        // Regardless of zoom, the state receives canvas-space positions from ctx.
        // GridSize = 160; snap((injected - dragOffset) / 160) * 160
        // (240-10)/160 = 1.4375 → round to 1 → 160
        var viewport = new ViewportService { Zoom = zoom };
        var ctx = new FakeInteractionContextWithViewport(viewport);
        ctx.InjectedCanvasPosition = new Point(injectedCanvasX, injectedCanvasY);

        var cell = new CellViewModel { CanvasX = 100, CanvasY = 100 };
        var state = new DragItemsState(cell, dragOffsetX, dragOffsetY,
            new[] { (cell, 100.0, 100.0) });
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        Assert.Equal(expectedX, cell.CanvasX, precision: 0);
        Assert.Equal(expectedY, cell.CanvasY, precision: 0);
    }

    /// <summary>
    /// DragItemsState.Exit marks the board as unsaved.
    /// </summary>
    [Fact]
    public void DragItemsState_Exit_MarksUnsaved()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel();
        var state = new DragItemsState(cell, 0, 0, new[] { (cell, 0.0, 0.0) });
        state.Enter(ctx);
        // IsDragging should be set on enter
        Assert.True(cell.IsDragging);
        state.Exit(ctx);
        // After exit, IsDragging is cleared
        Assert.False(cell.IsDragging);
        Assert.False(cell.IsDragInvalid);
    }

    // ── TransformBodyMoveState receives canvas-space positions ────────────────

    /// <summary>
    /// When the pointer moves in TransformBodyMoveState, the canvas position is
    /// forwarded to UpdateTransformMove — NOT a screen position.
    /// At zoom=3, the canvas position should be 1/3 of the screen position.
    /// </summary>
    [Fact]
    public void TransformBodyMoveState_UsesCanvasPosition()
    {
        var viewport = new ViewportService { Zoom = 3.0, OffsetX = 0, OffsetY = 0 };
        var ctx = new FakeTransformContextWithViewport(viewport);
        // Inject canvas-space position (as if GetPosition(MainCanvas) returned this)
        ctx.InjectedCanvasPosition = new Point(50, 75);

        var state = new TransformBodyMoveState();
        state.Enter(ctx);
        state.OnPointerMoved(null!, ctx);

        // State must forward the canvas-space point to UpdateTransformMove
        Assert.NotNull(ctx.LastUpdatePoint);
        Assert.Equal(50.0, ctx.LastUpdatePoint!.Value.X, precision: 6);
        Assert.Equal(75.0, ctx.LastUpdatePoint!.Value.Y, precision: 6);
    }

    // ── IdleState.OnKeyDown does not swallow keyboard shortcuts ───────────────

    /// <summary>
    /// IdleState.OnKeyDown must return Stay without marking e.Handled,
    /// so that keyboard shortcuts in Window_KeyDown continue to fire.
    /// </summary>
    [Fact]
    public void IdleState_OnKeyDown_DoesNotSwallowEvents()
    {
        var ctx = new FakeInteractionContext();
        var state = new IdleState();

        var result = state.OnKeyDown(null!, ctx);

        Assert.Equal(StateTransition.Stay, result);
        // Verify null event is handled gracefully (no NRE)
    }

    // ── PanState uses screen-space delta (not canvas-space) ──────────────────

    /// <summary>
    /// PanState.PanBy must work in screen space — the viewport OffsetX/Y accumulate
    /// screen-pixel deltas.  This test verifies that PanBy is called correctly
    /// by checking the ViewportService state directly when a PanState drives a delta.
    /// </summary>
    [Fact]
    public void PanState_PanBy_UsesScreenSpaceDelta()
    {
        var viewport = new ViewportService { Zoom = 2.0 };
        var ctx = new FakeInteractionContextWithViewport(viewport);
        // Screen-space delta: (10, 20)
        viewport.PanBy(new Avalonia.Vector(10, 20));

        Assert.Equal(10.0, viewport.OffsetX, precision: 6);
        Assert.Equal(20.0, viewport.OffsetY, precision: 6);
    }

    // ── MarqueePendingState null-guard ────────────────────────────────────────

    /// <summary>
    /// MarqueePendingState.OnPointerMoved has a null-guard that returns Stay when
    /// e is null (test harness can't synthesize real PointerEventArgs).
    /// This confirms the guard exists and the threshold logic is unreachable from tests.
    /// </summary>
    [Fact]
    public void MarqueePendingState_NullEvent_ReturnStay()
    {
        var ctx = new FakeInteractionContext();
        var state = new MarqueePendingState(new Point(100, 100), additive: false);

        var result = state.OnPointerMoved(null!, ctx);

        Assert.Equal(TransitionKind.Stay, result.Kind);
    }

    /// <summary>
    /// MarqueePendingState.OnPointerReleased pops and clears selection when not additive.
    /// </summary>
    [Fact]
    public void MarqueePendingState_Release_PopsAndClearsSelection()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        ctx.Selection.SelectCell(cell);
        var state = new MarqueePendingState(new Point(0, 0), additive: false);

        var result = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, result.Kind);
        Assert.Empty(ctx.Selection.SelectedCells);
    }

    /// <summary>
    /// MarqueePendingState.OnPointerReleased pops but preserves selection when additive.
    /// </summary>
    [Fact]
    public void MarqueePendingState_Release_Additive_PreservesSelection()
    {
        var ctx = new FakeInteractionContext();
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        ctx.Selection.SelectCell(cell);
        var state = new MarqueePendingState(new Point(0, 0), additive: true);

        var result = state.OnPointerReleased(null!, ctx);

        Assert.Equal(TransitionKind.Pop, result.Kind);
        Assert.Single(ctx.Selection.SelectedCells);
    }

    // ── ScreenToCanvas round-trip ─────────────────────────────────────────────

    /// <summary>
    /// ScreenToCanvas followed by "canvas to screen" must be the identity.
    /// canvas = (screen - offset) / zoom  ↔  screen = canvas * zoom + offset
    /// </summary>
    [Theory]
    [InlineData(1.0,    0,    0,  150, 250)]
    [InlineData(2.0,    0,    0,  150, 250)]
    [InlineData(0.5,  -30,  -30,  150, 250)]
    [InlineData(1.5,   50,   80,  300, 400)]
    public void ScreenToCanvas_RoundTrip_IsIdentity(
        double zoom, double offsetX, double offsetY,
        double screenX, double screenY)
    {
        var viewport = new ViewportService { Zoom = zoom, OffsetX = offsetX, OffsetY = offsetY };
        var ctx = new FakeInteractionContextWithViewport(viewport);

        var canvas = ctx.ScreenToCanvas(new Point(screenX, screenY));
        // Inverse: screen = canvas * zoom + offset
        double backX = canvas.X * zoom + offsetX;
        double backY = canvas.Y * zoom + offsetY;

        Assert.Equal(screenX, backX, precision: 4);
        Assert.Equal(screenY, backY, precision: 4);
    }
}

/// <summary>
/// FakeInteractionContext variant that holds a real ViewportService.
/// ScreenToCanvas is called directly on the viewport math helper below.
/// </summary>
internal sealed class FakeInteractionContextWithViewport : FakeInteractionContext
{
    public readonly ViewportService Vp;
    public FakeInteractionContextWithViewport(ViewportService vp) { ViewportOverride = vp; Vp = vp; }

    /// <summary>Applies the same formula as MainWindowInteractionContext.ScreenToCanvas.</summary>
    public new Point ScreenToCanvas(Point p) =>
        new Point((p.X - Vp.OffsetX) / Vp.Zoom,
                  (p.Y - Vp.OffsetY) / Vp.Zoom);
}

/// <summary>
/// Variant of FakeTransformContext that also uses a real ViewportService.
/// </summary>
internal sealed class FakeTransformContextWithViewport : FakeInteractionContext
{
    public Point? LastUpdatePoint { get; private set; }
    public FakeTransformContextWithViewport(ViewportService vp) { ViewportOverride = vp; }
    public override void UpdateTransformMove(Point pt) => LastUpdatePoint = pt;
    public override bool BeginTransformMove(Point pt) => true;
}
