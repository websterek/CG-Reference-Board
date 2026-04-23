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
}
