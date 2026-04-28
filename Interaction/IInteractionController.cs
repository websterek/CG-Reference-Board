using Avalonia.Input;

namespace CGReferenceBoard.Interaction;

/// <summary>
/// Single entry point for all pointer and keyboard events from the View.
/// Owns the current <see cref="IInteractionState"/> and drives transitions.
/// </summary>
public interface IInteractionController
{
    /// <summary>Current state (read-only for diagnostics/testing).</summary>
    IInteractionState CurrentState { get; }

    void OnPointerPressed(PointerPressedEventArgs e);
    void OnPointerMoved(PointerEventArgs e);
    void OnPointerReleased(PointerReleasedEventArgs e);
    void OnPointerCaptureLost(PointerCaptureLostEventArgs e);
    void OnKeyDown(KeyEventArgs e);
}
