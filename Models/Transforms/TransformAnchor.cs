namespace CGReferenceBoard.Models.Transforms;

/// <summary>
/// Identifies which handle on the transform box initiated the resize,
/// so each ITransformable can keep the opposite corner fixed.
/// </summary>
public enum TransformAnchor
{
    /// <summary>No resize handle — drag of the whole box, translation only.</summary>
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}
