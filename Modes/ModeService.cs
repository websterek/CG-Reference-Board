using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CGReferenceBoard.Modes.Abstractions;

namespace CGReferenceBoard.Modes;

/// <summary>
/// Event arguments for <see cref="ModeService.ModeChanged"/>.
/// </summary>
public sealed class ModeChangedEventArgs : EventArgs
{
    /// <summary>The mode that was active before the transition (null on first activation).</summary>
    public IMode? PreviousMode { get; init; }

    /// <summary>The mode that is now active.</summary>
    public IMode NewMode { get; init; } = null!;
}

/// <summary>
/// Manages the active application mode and transitions between modes.
/// Owns the singleton <see cref="GridMode"/> and <see cref="AnnotationMode"/> instances.
/// </summary>
public sealed partial class ModeService : ObservableObject
{
    // ── Singleton mode instances ──────────────────────────────────────────────

    /// <summary>The Grid layout mode instance.</summary>
    public GridMode GridMode { get; } = new();

    /// <summary>The Annotation drawing mode instance.</summary>
    public AnnotationMode AnnotationMode { get; } = new();

    /// <summary>All available modes in display order.</summary>
    public ObservableCollection<IMode> AvailableModes { get; }

    // ── Current mode (source-generated observable property) ──────────────────

    /// <summary>
    /// The currently active mode. Raises <see cref="ModeChanged"/> and notifies
    /// <see cref="IsGridMode"/> / <see cref="IsAnnotationMode"/> on change.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridMode))]
    [NotifyPropertyChangedFor(nameof(IsAnnotationMode))]
    private IMode _currentMode;

    // ── Computed flags ────────────────────────────────────────────────────────

    /// <summary>True when <see cref="CurrentMode"/> is <see cref="GridMode"/>.</summary>
    public bool IsGridMode => CurrentMode is GridMode;

    /// <summary>True when <see cref="CurrentMode"/> is <see cref="AnnotationMode"/>.</summary>
    public bool IsAnnotationMode => CurrentMode is AnnotationMode;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised after a mode transition completes.</summary>
    public event EventHandler<ModeChangedEventArgs>? ModeChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ModeService()
    {
        AvailableModes = new ObservableCollection<IMode> { GridMode, AnnotationMode };
        _currentMode = GridMode; // start in Grid mode; field set directly to avoid firing event in ctor
    }

    // ── Mode transitions ──────────────────────────────────────────────────────

    /// <summary>
    /// Transitions to the specified mode. No-ops if <paramref name="mode"/> is already active.
    /// Calls <see cref="IMode.Deactivate"/> on the outgoing mode and <see cref="IMode.Activate"/>
    /// on the incoming mode, then fires <see cref="ModeChanged"/>.
    /// </summary>
    public void SetMode(IMode mode)
    {
        if (mode == CurrentMode) return;

        var previous = CurrentMode;
        previous.Deactivate();
        CurrentMode = mode;
        mode.Activate();

        ModeChanged?.Invoke(this, new ModeChangedEventArgs
        {
            PreviousMode = previous,
            NewMode = mode
        });
    }

    /// <summary>
    /// Transitions to the mode identified by <paramref name="modeName"/>.
    /// Unrecognised names fall back to <see cref="GridMode"/>.
    /// </summary>
    public void SetMode(string modeName)
    {
        IMode target = modeName switch
        {
            "Annotation" => AnnotationMode,
            _ => GridMode
        };
        SetMode(target);
    }
}
