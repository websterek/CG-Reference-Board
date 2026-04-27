using Avalonia.Media.Imaging;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class ViewportService : IViewportService
{
    public double Zoom { get; set; } = 1.0;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public BitmapInterpolationMode InterpolationMode { get; set; } = BitmapInterpolationMode.LowQuality;
    public double ZoomInverseFactor => 1.0 / Zoom;
}