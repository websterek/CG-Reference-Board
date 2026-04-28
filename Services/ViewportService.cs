using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
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

    public void ZoomAt(Point anchor, double factor)
    {
        double newZoom = Math.Clamp(_zoom * factor, 0.05, 50.0);
        double actualFactor = newZoom / _zoom;
        OffsetX = anchor.X - actualFactor * (anchor.X - _offsetX);
        OffsetY = anchor.Y - actualFactor * (anchor.Y - _offsetY);
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
        double scaleX = viewportSize.Width / boardBounds.Width;
        double scaleY = viewportSize.Height / boardBounds.Height;
        double newZoom = Math.Clamp(Math.Min(scaleX, scaleY) * 0.9, 0.05, 50.0);
        Zoom = newZoom;
        OffsetX = (viewportSize.Width - boardBounds.Width * newZoom) / 2 - boardBounds.X * newZoom;
        OffsetY = (viewportSize.Height - boardBounds.Height * newZoom) / 2 - boardBounds.Y * newZoom;
    }
}
