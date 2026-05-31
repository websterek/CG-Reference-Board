using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;
using CGReferenceBoard.Views;

namespace CGReferenceBoard.Interaction;

/// <summary>
/// Live implementation of IInteractionContext backed by MainWindow and its services.
/// </summary>
public sealed class MainWindowInteractionContext : IInteractionContext
{
    private readonly MainWindow _window;
    private readonly IViewportService _viewport;
    private Canvas? _mainCanvas;

    public MainWindowInteractionContext(MainWindow window, IViewportService viewport)
    {
        _window = window;
        _viewport = viewport;
    }

    /// <summary>
    /// Returns the MainCanvas visual, lazily looked up and cached.
    /// Used to convert pointer events into canvas-space coordinates via Avalonia's
    /// built-in inverse-transform hit-testing (GetPosition accounts for ScaleTransform).
    /// </summary>
    private Canvas? MainCanvas =>
        _mainCanvas ??= _window.FindControl<Canvas>("MainCanvas");

    public MainWindowViewModel Vm => _window.Vm;
    public SelectionService Selection => _window.Vm.SelectionService;
    public IViewportService Viewport => _viewport;
    public IHistoryService History => null!; // TODO: inject in future

    public Point ScreenToCanvas(Point screenPt)
    {
        return new Point(
            (screenPt.X - _viewport.OffsetX) / _viewport.Zoom,
            (screenPt.Y - _viewport.OffsetY) / _viewport.Zoom);
    }

    public Point GetCanvasPosition(PointerEventArgs e)
    {
        // GetPosition(MainCanvas) returns a point in canvas-space coordinates:
        // Avalonia applies the inverse of MainCanvas's accumulated RenderTransform
        // (Translate + Scale), so the result is already in canvas units regardless
        // of the current zoom level.  This is equivalent to:
        //   (windowRelativePos - containerOffset) / zoom - offsetX
        // but handles nested transforms and window chrome automatically.
        var canvas = MainCanvas;
        if (canvas != null)
            return e.GetPosition(canvas);

        // Fallback for tests or before the canvas is attached: manual conversion
        return ScreenToCanvas(e.GetPosition(null));
    }

    public Point GetScreenPosition(PointerEventArgs e)
    {
        return e?.GetPosition(null) ?? default;
    }

    public CellViewModel? HitTestCell(Point canvasPt)
    {
        return null; // TODO: wire to spatial index
    }

    public IReadOnlyList<CellViewModel> HitTestCellsInRect(Rect canvasRect)
    {
        return _window.Vm.GridCells
            .Where(c =>
            {
                var bounds = new Rect(c.CanvasX, c.CanvasY, c.ColSpan * 120.0, c.RowSpan * 120.0);
                var intersection = bounds.Intersect(canvasRect);
                return intersection.Width > 0 && intersection.Height > 0;
            })
            .ToList();
    }

    public IReadOnlyList<AnnotationViewModel> HitTestAnnotationsInRect(Rect canvasRect) =>
        Array.Empty<AnnotationViewModel>();

    public void SetAnnotationMarqueeRect(Rect? rect) { }
    public void SetCellMarqueeRect(Rect? rect) { }

    public void SetPointerCapture(IPointer? pointer, bool capture)
    {
        var canvasBorder = _window.FindControl<Border>("CanvasBorder");
        if (canvasBorder is null) return;
        if (capture) pointer?.Capture(canvasBorder);
        else pointer?.Capture(null);
    }

    public void RequestViewportUpdate() =>
        _window.ScheduleViewportUpdate();

    public void NotifyZoomChanged() { }

    public bool BeginTransformMove(Point canvasPt) =>
        _window.StartTransformMoveFromCurrentSelection(canvasPt);

    public void UpdateTransformMove(Point canvasPt) =>
        _window.UpdateActiveTransform(canvasPt);

    public void FinishTransformMove() =>
        _window.FinishActiveTransformFromState();

    // ── Marquee selection ─────────────────────────────────────────────────────

    public void BeginAnnotationMarquee(Point canvasPt, bool additive) =>
        _window.BeginAnnotationMarquee(canvasPt, additive);

    public void UpdateAnnotationMarquee(Point canvasPt) =>
        _window.UpdateAnnotationMarqueeFromState(canvasPt);

    public void FinishAnnotationMarquee() =>
        _window.FinishAnnotationMarqueeFromState();

    public void BeginCellMarquee(Point canvasPt, bool additive) =>
        _window.BeginCellMarquee(canvasPt, additive);

    public void UpdateCellMarquee(Point canvasPt) =>
        _window.UpdateCellMarqueeFromState(canvasPt);

    public void FinishCellMarquee() =>
        _window.FinishCellMarqueeFromState();

    // ── Backdrop placement preview ────────────────────────────────────────────

    public bool IsShowingPlacementPreview => _window.IsShowingPlacementPreview;

    public void UpdatePlacementPreview(Point canvasPt) =>
        _window.UpdatePlacementPreview(canvasPt);

    public bool TryPlacePendingBackdrop() =>
        _window.TryPlacePendingBackdrop();

    public void HidePlacementPreview() =>
        _window.HidePlacementPreview();

    public void ShakeScreen() =>
        _window.ShakeScreen();

    // ── Transform body ────────────────────────────────────────────────────────

    public bool TryBeginTransformBodyMove(Point canvasPt) =>
        _window.TryStartTransformBodyMoveInternal(canvasPt);

    // ── Draw-mode ─────────────────────────────────────────────────────────────

    public AnnotationViewModel? BeginDrawAnnotation(Point canvasPt) =>
        _window.BeginDrawAnnotationInternal(canvasPt);

    public void FinishDrawAnnotation() => _window.FinishDrawAnnotationInternal();
}
