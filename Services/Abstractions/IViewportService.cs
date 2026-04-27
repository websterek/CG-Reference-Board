namespace CGReferenceBoard.Services.Abstractions;

public interface IViewportService
{
    double Zoom { get; set; }
    double OffsetX { get; set; }
    double OffsetY { get; set; }
    Avalonia.Media.Imaging.BitmapInterpolationMode InterpolationMode { get; }
    double ZoomInverseFactor { get; }
}