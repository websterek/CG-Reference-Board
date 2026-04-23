using CommunityToolkit.Mvvm.ComponentModel;
using CGReferenceBoard.Modes.Abstractions;

namespace CGReferenceBoard.Modes;

/// <summary>
/// Annotation drawing mode. Tracks the currently active drawing tool and exposes
/// per-tool boolean flags for XAML menu-checkmark bindings.
/// </summary>
public sealed partial class AnnotationMode : ObservableObject, IMode
{
    /// <inheritdoc/>
    public string Name => "Annotation";

    /// <inheritdoc/>
    public string DisplayName => "Annotation Mode";

    // ── Current tool ─────────────────────────────────────────────────────────

    /// <summary>
    /// The active annotation tool. One of: Brush, Text, Arrow, Rectangle, Ellipse, Eraser, Move.
    /// Changing this property raises change notifications for all Is*Selected flags.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrushSelected))]
    [NotifyPropertyChangedFor(nameof(IsTextSelected))]
    [NotifyPropertyChangedFor(nameof(IsArrowSelected))]
    [NotifyPropertyChangedFor(nameof(IsRectangleSelected))]
    [NotifyPropertyChangedFor(nameof(IsEllipseSelected))]
    [NotifyPropertyChangedFor(nameof(IsEraserSelected))]
    [NotifyPropertyChangedFor(nameof(IsMoveSelected))]
    [NotifyPropertyChangedFor(nameof(IsEraserMode))]
    [NotifyPropertyChangedFor(nameof(IsMoveMode))]
    private string _currentTool = "Brush";

    // ── Tool selection flags (for menu checkmarks / toolbar highlights) ───────

    /// <summary>True when the Brush tool is active.</summary>
    public bool IsBrushSelected => CurrentTool == "Brush";

    /// <summary>True when the Text tool is active.</summary>
    public bool IsTextSelected => CurrentTool == "Text";

    /// <summary>True when the Arrow tool is active.</summary>
    public bool IsArrowSelected => CurrentTool == "Arrow";

    /// <summary>True when the Rectangle tool is active.</summary>
    public bool IsRectangleSelected => CurrentTool == "Rectangle";

    /// <summary>True when the Ellipse tool is active.</summary>
    public bool IsEllipseSelected => CurrentTool == "Ellipse";

    /// <summary>True when the Eraser tool is active.</summary>
    public bool IsEraserSelected => CurrentTool == "Eraser";

    /// <summary>True when the Move tool is active.</summary>
    public bool IsMoveSelected => CurrentTool == "Move";

    // ── Convenience mode flags ────────────────────────────────────────────────

    /// <summary>True when the eraser tool is active (alias for <see cref="IsEraserSelected"/>).</summary>
    public bool IsEraserMode => CurrentTool == "Eraser";

    /// <summary>True when the move tool is active (alias for <see cref="IsMoveSelected"/>).</summary>
    public bool IsMoveMode => CurrentTool == "Move";

    // ── IMode ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Activate() { /* future: configure canvas cursor, enable annotation layer */ }

    /// <inheritdoc/>
    public void Deactivate()
    {
        // Reset to default tool when leaving annotation mode so re-entry starts clean.
        CurrentTool = "Brush";
    }
}
