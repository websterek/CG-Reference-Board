using Avalonia.Media;
using CGReferenceBoard.Layers.Abstractions;

namespace CGReferenceBoard.Layers.Background;

public sealed class NoneBackgroundRenderer : IBackgroundRenderer
{
    public string Key => "None";
    public IBrush? CreateBrush() => null;
}
