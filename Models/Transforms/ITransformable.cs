namespace CGReferenceBoard.Models.Transforms;

/// <summary>
/// Implemented by CellViewModel and AnnotationViewModel so the generic
/// TransformBoxControl can move and resize items without knowing their type.
/// All coordinates are in canvas space (pixels).
/// </summary>
public interface ITransformable
{
    /// <summary>Left edge of the visual bounding box in canvas pixels.</summary>
    double BoundsLeft { get; }

    /// <summary>Top edge of the visual bounding box in canvas pixels.</summary>
    double BoundsTop { get; }

    /// <summary>Width of the visual bounding box in canvas pixels.</summary>
    double BoundsWidth { get; }

    /// <summary>Height of the visual bounding box in canvas pixels.</summary>
    double BoundsHeight { get; }

    /// <summary>Whether this item supports resize (not just move).</summary>
    bool CanResize { get; }

    /// <summary>
    /// Move so the visual top-left corner is at (canvasX, canvasY).
    /// Grid-snapping (if any) is applied by the caller (IMode.ApplyTransform).
    /// </summary>
    void MoveTo(double canvasX, double canvasY);

    /// <summary>
    /// Resize to the given pixel dimensions, keeping the specified anchor fixed.
    /// Grid-snapping is applied by the caller before this is called.
    /// </summary>
    void ResizeTo(double newWidth, double newHeight, TransformAnchor anchor);
}
