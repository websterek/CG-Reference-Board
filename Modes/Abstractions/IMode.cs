using CGReferenceBoard.Models.Transforms;

namespace CGReferenceBoard.Modes.Abstractions;

/// <summary>
/// Represents an application interaction mode (e.g. Grid layout or Annotation drawing).
/// Modes are activated/deactivated by <see cref="CGReferenceBoard.Modes.ModeService"/>.
/// </summary>
public interface IMode
{
    /// <summary>Stable programmatic identifier, e.g. "Grid" or "Annotation".</summary>
    string Name { get; }

    /// <summary>Human-readable label shown in the UI, e.g. "Grid Mode".</summary>
    string DisplayName { get; }

    /// <summary>Called by <see cref="CGReferenceBoard.Modes.ModeService"/> when this mode becomes active.</summary>
    void Activate();

    /// <summary>Called by <see cref="CGReferenceBoard.Modes.ModeService"/> when this mode is deactivated.</summary>
    void Deactivate();

    /// <summary>
    /// Applies a move or resize transform to <paramref name="target"/>, enforcing
    /// any mode-specific constraints (e.g. grid snapping in Grid mode) before
    /// delegating to <see cref="ITransformable.MoveTo"/> or
    /// <see cref="ITransformable.ResizeTo"/>.
    /// </summary>
    /// <param name="target">The item being transformed.</param>
    /// <param name="anchor">
    /// <see cref="TransformAnchor.None"/> indicates a move operation (uses
    /// <paramref name="newLeft"/> and <paramref name="newTop"/>).
    /// Any other value indicates a resize with that anchor held fixed (uses
    /// <paramref name="newWidth"/> and <paramref name="newHeight"/>).
    /// </param>
    /// <param name="newLeft">Desired new left edge in canvas pixels (move operations).</param>
    /// <param name="newTop">Desired new top edge in canvas pixels (move operations).</param>
    /// <param name="newWidth">Desired new width in canvas pixels (resize operations).</param>
    /// <param name="newHeight">Desired new height in canvas pixels (resize operations).</param>
    void ApplyTransform(
        ITransformable target,
        TransformAnchor anchor,
        double newLeft,
        double newTop,
        double newWidth,
        double newHeight);
}
