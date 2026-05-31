using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Interaction;

/// <summary>
/// Service facade passed to every <see cref="IInteractionState"/>.
/// Provides access to viewport, selection, board data, and UI helpers
/// without coupling states to the MainWindow directly.
/// </summary>
public interface IInteractionContext
{
    MainWindowViewModel Vm { get; }
    SelectionService Selection { get; }
    IViewportService Viewport { get; }

    /// <summary>Translates a screen-space point to canvas coordinates.</summary>
    Point ScreenToCanvas(Point screenPt);

    /// <summary>
    /// Returns the canvas-space position of a pointer event.
    /// Equivalent to ScreenToCanvas(e.GetPosition(null)), but allows
    /// test fakes to inject arbitrary positions without real event args.
    /// </summary>
    Point GetCanvasPosition(PointerEventArgs e);

    /// <summary>
    /// Returns the screen-space position of a pointer event.
    /// Equivalent to e.GetPosition(null), but allows test fakes to inject
    /// arbitrary positions without real event args.
    /// </summary>
    Point GetScreenPosition(PointerEventArgs e);

    IHistoryService History { get; }

    CellViewModel? HitTestCell(Point canvasPt);
    IReadOnlyList<CellViewModel> HitTestCellsInRect(Rect canvasRect);
    IReadOnlyList<AnnotationViewModel> HitTestAnnotationsInRect(Rect canvasRect);

    void SetAnnotationMarqueeRect(Rect? rect);
    void SetCellMarqueeRect(Rect? rect);
    void SetPointerCapture(IPointer? pointer, bool capture);
    void RequestViewportUpdate();
    void NotifyZoomChanged();

    /// <summary>Begins a transform-move operation from the current selection.</summary>
    bool BeginTransformMove(Point canvasPt);
    /// <summary>Updates position during an active transform-move operation.</summary>
    void UpdateTransformMove(Point canvasPt);
    /// <summary>Commits or reverts the active transform operation on pointer release.</summary>
    void FinishTransformMove();

    // ── Marquee selection ─────────────────────────────────────────────────────

    /// <summary>Starts an annotation-marquee drag at the given canvas point.</summary>
    void BeginAnnotationMarquee(Point canvasPt, bool additive);
    /// <summary>Updates the annotation-marquee rubber-band as the pointer moves.</summary>
    void UpdateAnnotationMarquee(Point canvasPt);
    /// <summary>Finalises the annotation-marquee: selects intersecting annotations and hides the marquee.</summary>
    void FinishAnnotationMarquee();

    /// <summary>Starts a cell-marquee drag at the given canvas point.</summary>
    void BeginCellMarquee(Point canvasPt, bool additive);
    /// <summary>Updates the cell-marquee rubber-band as the pointer moves.</summary>
    void UpdateCellMarquee(Point canvasPt);
    /// <summary>Finalises the cell-marquee: selects intersecting cells and hides the marquee.</summary>
    void FinishCellMarquee();

    // ── Backdrop placement preview ────────────────────────────────────────────

    /// <summary>True while a backdrop is waiting to be placed on the canvas.</summary>
    bool IsShowingPlacementPreview { get; }
    /// <summary>Moves the placement-preview ghost to the grid cell under <paramref name="canvasPt"/>.</summary>
    void UpdatePlacementPreview(Point canvasPt);
    /// <summary>Commits the pending backdrop at the current preview position. Returns false if the position is invalid.</summary>
    bool TryPlacePendingBackdrop();
    /// <summary>Cancels the pending backdrop placement and hides the preview.</summary>
    void HidePlacementPreview();
    /// <summary>Triggers the shake-screen visual feedback (invalid placement).</summary>
    void ShakeScreen();

    // ── Transform body ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the canvas point is inside the transform body (deflated by handle size),
    /// LMB is pressed, and CanMove is allowed. Also begins the move operation.
    /// </summary>
    bool TryBeginTransformBodyMove(Point canvasPt);

    // ── Draw-mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new AnnotationViewModel for the current tool/color/thickness,
    /// adds the initial point, and adds it to Vm.Annotations.
    /// Returns null for the Text tool (handled separately via the text editor overlay).
    /// </summary>
    AnnotationViewModel? BeginDrawAnnotation(Point canvasPt);

    /// <summary>
    /// Finalises the current draw operation: clears the in-progress annotation reference
    /// and marks the board as unsaved.
    /// </summary>
    void FinishDrawAnnotation();
}

