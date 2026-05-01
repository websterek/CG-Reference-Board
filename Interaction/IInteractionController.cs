using System;
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

    /// <summary>
    /// Fired when the interaction context changes (mode or tool switch) so the
    /// View can cancel any in-progress transform/drag. Replaces
    /// <c>MainWindowViewModel.TransformContextChanging</c>.
    /// </summary>
    event Action? TransformContextChanging;

    void OnPointerPressed(PointerPressedEventArgs e);
    void OnPointerMoved(PointerEventArgs e);
    void OnPointerReleased(PointerReleasedEventArgs e);
    void OnPointerCaptureLost(PointerCaptureLostEventArgs e);
    void OnKeyDown(KeyEventArgs e);

    /// <summary>Notify listeners that the interaction context has changed.</summary>
    void NotifyTransformContextChanging();
}
