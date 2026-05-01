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

    public MainWindowInteractionContext(MainWindow window, IViewportService viewport)
    {
        _window = window;
        _viewport = viewport;
    }

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
        return ScreenToCanvas(e.GetPosition(null));
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
}
