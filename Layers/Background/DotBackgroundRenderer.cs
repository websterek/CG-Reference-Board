using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CGReferenceBoard.Layers.Abstractions;

namespace CGReferenceBoard.Layers.Background;

public sealed class DotBackgroundRenderer : IBackgroundRenderer
{
    public string Key => "Dots";

    public IBrush? CreateBrush()
    {
        return new VisualBrush
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, 160, 160, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 160, 160, RelativeUnit.Absolute),
            Visual = new Canvas
            {
                Width = 160,
                Height = 160,
                Children =
                {
                    new Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = SolidColorBrush.Parse("#3D3D3D"),
                        [Canvas.LeftProperty] = 77.0,
                        [Canvas.TopProperty] = 77.0
                    }
                }
            }
        };
    }
}
