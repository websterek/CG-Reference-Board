using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Single source of truth for pan/zoom state. Fires INPC on every change
/// so View bindings (RenderTransform, zoom-dependent templates) stay in sync.
/// </summary>
public interface IViewportService : INotifyPropertyChanged
{
    // ── State ────────────────────────────────────────────────────────────────

    double Zoom { get; set; }
    double OffsetX { get; set; }
    double OffsetY { get; set; }
    BitmapInterpolationMode InterpolationMode { get; set; }

    // ── Derived (read-only) ──────────────────────────────────────────────────

    double ZoomInverseFactor { get; }
    string ZoomLevelText { get; }
    bool IsCanvasBackgroundVisible { get; }

    // ── Operations ───────────────────────────────────────────────────────────

    /// <summary>Translates viewport by <paramref name="delta"/> in screen coordinates.</summary>
    void PanBy(Vector delta);

    /// <summary>Zooms in/out by <paramref name="factor"/> keeping <paramref name="anchor"/> fixed on screen.</summary>
    void ZoomAt(Point anchor, double factor);

    /// <summary>Resets zoom to 1.0 and offset to (0, 0).</summary>
    void ResetView();

    /// <summary>Fits the viewport to show all content within <paramref name="boardBounds"/>.</summary>
    void FitToBoard(Rect boardBounds, Size viewportSize);
}
