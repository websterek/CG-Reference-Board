namespace CGReferenceBoard.Models.Transforms;

/// <summary>
/// Identifies which handle on the transform box initiated the resize,
/// so each ITransformable can keep the opposite corner fixed.
/// </summary>
public enum TransformAnchor
{
    None,        // drag of the whole box — translation only
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}
