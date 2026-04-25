using System;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models.Transforms;
using CGReferenceBoard.Modes.Abstractions;

namespace CGReferenceBoard.Modes;

/// <summary>
/// Grid layout mode — the default application mode for placing and arranging cells.
/// </summary>
public sealed class GridMode : IMode
{
    /// <inheritdoc/>
    public string Name => "Grid";

    /// <inheritdoc/>
    public string DisplayName => "Grid Mode";

    /// <inheritdoc/>
    public void Activate() { /* future: configure tool availability */ }

    /// <inheritdoc/>
    public void Deactivate() { /* future: teardown */ }

    /// <inheritdoc/>
    /// <remarks>
    /// All coordinates and dimensions are snapped to the nearest
    /// <see cref="Constants.GridSize"/> boundary before being applied,
    /// with a minimum size of one grid unit.
    /// </remarks>
    public void ApplyTransform(
        ITransformable target,
        TransformAnchor anchor,
        double newLeft,
        double newTop,
        double newWidth,
        double newHeight)
    {
        if (anchor == TransformAnchor.None)
        {
            // Move: snap position to nearest grid intersection.
            double snappedLeft = Math.Round(newLeft / Constants.GridSize) * Constants.GridSize;
            double snappedTop  = Math.Round(newTop  / Constants.GridSize) * Constants.GridSize;
            target.MoveTo(snappedLeft, snappedTop);
        }
        else
        {
            // Resize: snap dimensions to nearest grid unit, min one cell.
            double snappedWidth  = Math.Max(Constants.GridSize,
                Math.Round(newWidth  / Constants.GridSize) * Constants.GridSize);
            double snappedHeight = Math.Max(Constants.GridSize,
                Math.Round(newHeight / Constants.GridSize) * Constants.GridSize);
            target.ResizeTo(snappedWidth, snappedHeight, anchor);
        }
    }
}
