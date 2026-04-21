using Avalonia.Media;

namespace CGReferenceBoard.Layers.Abstractions;

public interface IBackgroundRenderer
{
    string Key { get; }
    IBrush? CreateBrush();
}
