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
}
