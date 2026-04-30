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

    IHistoryService History { get; }

    CellViewModel? HitTestCell(Point canvasPt);
    IReadOnlyList<CellViewModel> HitTestCellsInRect(Rect canvasRect);
    IReadOnlyList<AnnotationViewModel> HitTestAnnotationsInRect(Rect canvasRect);

    void SetAnnotationMarqueeRect(Rect? rect);
    void SetCellMarqueeRect(Rect? rect);
    void SetPointerCapture(IPointer? pointer, bool capture);
    void RequestViewportUpdate();
    void NotifyZoomChanged();
}
