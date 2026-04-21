namespace CGReferenceBoard.Layers.Abstractions;

public static class LayerZOrder
{
    public const int Background = -100;
    public const int Backdrops = -10;
    public const int Items = 0;
    public const int Labels = 10;
    public const int Annotations = 200;
    public const int Overlays = 1000;

    public const int DragBoost = 100;

    public const int BackdropDragging = Backdrops + DragBoost;
    public const int ItemDragging = Items + DragBoost + 20;
    public const int LabelDragging = Labels + DragBoost + 40;
}
