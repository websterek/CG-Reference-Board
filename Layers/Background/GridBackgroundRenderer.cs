using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using CGReferenceBoard.Layers.Abstractions;

namespace CGReferenceBoard.Layers.Background;

public sealed class GridBackgroundRenderer : IBackgroundRenderer
{
    public string Key => "Grid";

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
                    new Rectangle
                    {
                        Width = 160,
                        Height = 0.5,
                        Fill = SolidColorBrush.Parse("#282828"),
                        [Canvas.LeftProperty] = 0.0,
                        [Canvas.TopProperty] = 0.0
                    },
                    new Rectangle
                    {
                        Width = 0.5,
                        Height = 160,
                        Fill = SolidColorBrush.Parse("#282828"),
                        [Canvas.LeftProperty] = 0.0,
                        [Canvas.TopProperty] = 0.0
                    }
                }
            }
        };
    }
}
