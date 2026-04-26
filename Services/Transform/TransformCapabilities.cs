namespace CGReferenceBoard.Services.Transform;

public sealed record TransformCapabilities(
    bool CanMove,
    bool CanResize,
    bool UsesGridSnapping,
    bool UsesCollisionChecks)
{
    public static TransformCapabilities None { get; } = new(false, false, false, false);
    public static TransformCapabilities Grid { get; } = new(true, true, true, true);
    public static TransformCapabilities Annotation { get; } = new(true, true, false, false);

    public bool AllowsOperation(TransformOperation operation)
        => operation switch
        {
            TransformOperation.Move => CanMove,
            TransformOperation.Resize => CanResize,
            _ => false
        };
}
