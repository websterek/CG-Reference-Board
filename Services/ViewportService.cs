using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public sealed class ViewportService : IViewportService
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(_zoom - value) < 1e-9) return;
            _zoom = value;
            Notify(nameof(Zoom));
            Notify(nameof(ZoomInverseFactor));
            Notify(nameof(ZoomLevelText));
            Notify(nameof(IsCanvasBackgroundVisible));
        }
    }

    private double _offsetX;
    public double OffsetX
    {
        get => _offsetX;
        set { if (Math.Abs(_offsetX - value) < 1e-9) return; _offsetX = value; Notify(nameof(OffsetX)); }
    }

    private double _offsetY;
    public double OffsetY
    {
        get => _offsetY;
        set { if (Math.Abs(_offsetY - value) < 1e-9) return; _offsetY = value; Notify(nameof(OffsetY)); }
    }

    private BitmapInterpolationMode _interpolationMode = BitmapInterpolationMode.LowQuality;
    public BitmapInterpolationMode InterpolationMode
    {
        get => _interpolationMode;
        set { _interpolationMode = value; Notify(nameof(InterpolationMode)); }
    }

    public double ZoomInverseFactor => 1.0 / _zoom;
    public string ZoomLevelText => $"{_zoom * 100:F0}%";
    public bool IsCanvasBackgroundVisible => _zoom >= 0.25;

    public void PanBy(Vector delta)
    {
        OffsetX += delta.X;
        OffsetY += delta.Y;
    }

    public void ZoomAt(Point screenAnchor, double factor)
    {
        double oldZoom = _zoom;
        double newZoom = Math.Clamp(oldZoom * factor, Constants.MinZoom, Constants.MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9) return;
        // Translate→Scale math: screen = (canvas + tx) * scale
        // Keep screenAnchor fixed: tx_new = tx_old + screenAnchor * (1/newZoom - 1/oldZoom)
        OffsetX += screenAnchor.X * (1.0 / newZoom - 1.0 / oldZoom);
        OffsetY += screenAnchor.Y * (1.0 / newZoom - 1.0 / oldZoom);
        Zoom = newZoom;
    }

    public void ResetView()
    {
        OffsetX = 0;
        OffsetY = 0;
        Zoom = 1.0;
    }

    public void FitToBoard(Rect boardBounds, Size viewportSize)
    {
        if (boardBounds.Width <= 0 || boardBounds.Height <= 0 || viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            ResetView();
            return;
        }
        // Translate→Scale math: tx = viewW / (2 * zoom) - (minX + maxX) / 2
        double scaleX = viewportSize.Width / boardBounds.Width;
        double scaleY = viewportSize.Height / boardBounds.Height;
        double newZoom = Math.Clamp(Math.Min(scaleX, scaleY), Constants.MinZoom, 2.0);
        double minX = boardBounds.X;
        double maxX = boardBounds.Right;
        double minY = boardBounds.Y;
        double maxY = boardBounds.Bottom;
        Zoom = newZoom;
        OffsetX = viewportSize.Width / 2.0 / newZoom - (minX + maxX) / 2.0;
        OffsetY = viewportSize.Height / 2.0 / newZoom - (minY + maxY) / 2.0;
    }

    // ── LOD refresh request ──────────────────────────────────────────────────

    public event Action? RefreshRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke();
}
