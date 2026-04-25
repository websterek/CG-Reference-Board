using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CGReferenceBoard.Models.Transforms;
using CGReferenceBoard.Modes;
using CGReferenceBoard.Services;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Tracks the union bounding box of the current selection and exposes a
/// mode-aware <see cref="ApplyTransform"/> method consumed by
/// <c>TransformBoxControl</c>.
/// </summary>
/// <remarks>
/// <para>
/// The adorner subscribes to both <see cref="SelectionService.SelectedCells"/>
/// and <see cref="SelectionService.SelectedAnnotations"/> and recomputes its
/// bounds whenever the selection or any selected item's bounds change.
/// </para>
/// <para>
/// Move operations distribute the same delta to every selected item.
/// Resize operations are forwarded only when exactly one resizable item is
/// selected (<see cref="CanResize"/> is <see langword="true"/>).
/// </para>
/// </remarks>
public sealed partial class TransformAdornerViewModel : ObservableObject
{
    private readonly SelectionService _selection;
    private readonly ModeService _modeService;

    // ── Computed union bounding box (canvas pixels) ───────────────────────────

    /// <summary>Left edge of the selection's union bounding box.</summary>
    [ObservableProperty]
    private double _left;

    /// <summary>Top edge of the selection's union bounding box.</summary>
    [ObservableProperty]
    private double _top;

    /// <summary>Width of the selection's union bounding box.</summary>
    [ObservableProperty]
    private double _width;

    /// <summary>Height of the selection's union bounding box.</summary>
    [ObservableProperty]
    private double _height;

    // ── State flags ───────────────────────────────────────────────────────────

    /// <summary>
    /// <see langword="true"/> when at least one item is selected and the
    /// transform box should be shown.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// <see langword="true"/> when exactly one item is selected and that item
    /// supports resize.  When <see langword="false"/> the control shows only
    /// the centre move handle.
    /// </summary>
    [ObservableProperty]
    private bool _canResize;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the adorner and subscribes to the selection collections.
    /// </summary>
    public TransformAdornerViewModel(SelectionService selection, ModeService modeService)
    {
        _selection   = selection;
        _modeService = modeService;

        _selection.SelectedCells.CollectionChanged       += OnSelectionChanged;
        _selection.SelectedAnnotations.CollectionChanged += OnSelectionChanged;

        Recompute();
    }

    // ── Selection change handling ─────────────────────────────────────────────

    private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Unsubscribe from items that left the selection.
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged -= OnItemPropertyChanged;
        }

        // Subscribe to items that entered the selection.
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += OnItemPropertyChanged;
        }

        Recompute();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Recompute only when a bounds-relevant property changes.
        if (e.PropertyName is nameof(ITransformable.BoundsLeft)
                           or nameof(ITransformable.BoundsTop)
                           or nameof(ITransformable.BoundsWidth)
                           or nameof(ITransformable.BoundsHeight))
        {
            Recompute();
        }
    }

    // ── Bounding box computation ──────────────────────────────────────────────

    private void Recompute()
    {
        var items = CollectTransformables();

        if (items.Count == 0)
        {
            IsVisible = false;
            Left = 0;
            Top  = 0;
            Width  = 0;
            Height = 0;
            CanResize = false;
            return;
        }

        double minLeft   = double.MaxValue;
        double minTop    = double.MaxValue;
        double maxRight  = double.MinValue;
        double maxBottom = double.MinValue;

        foreach (var item in items)
        {
            if (item.BoundsLeft < minLeft)  minLeft  = item.BoundsLeft;
            if (item.BoundsTop  < minTop)   minTop   = item.BoundsTop;

            double right  = item.BoundsLeft + item.BoundsWidth;
            double bottom = item.BoundsTop  + item.BoundsHeight;
            if (right  > maxRight)  maxRight  = right;
            if (bottom > maxBottom) maxBottom = bottom;
        }

        Left   = minLeft;
        Top    = minTop;
        Width  = maxRight  - minLeft;
        Height = maxBottom - minTop;
        IsVisible = true;
        CanResize = items.Count == 1 && items[0].CanResize;
    }

    /// <summary>
    /// Returns the current selection as a flat list of <see cref="ITransformable"/>.
    /// Both <c>CellViewModel</c> and <c>AnnotationViewModel</c> implement the interface.
    /// </summary>
    private List<ITransformable> CollectTransformables()
    {
        var list = new List<ITransformable>(
            _selection.SelectedCells.Count + _selection.SelectedAnnotations.Count);

        foreach (var cell in _selection.SelectedCells)
            if (cell is ITransformable t) list.Add(t);

        foreach (var ann in _selection.SelectedAnnotations)
            if (ann is ITransformable t) list.Add(t);

        return list;
    }

    // ── Transform application ─────────────────────────────────────────────────

    /// <summary>
    /// Applies a move or resize transform to the current selection using the
    /// active mode's constraints (e.g. grid snapping).
    /// </summary>
    /// <param name="anchor">
    /// <see cref="TransformAnchor.None"/> triggers a move: each selected item
    /// is shifted by the same delta relative to its current position.
    /// Any other value triggers a resize (valid only when
    /// <see cref="CanResize"/> is <see langword="true"/>).
    /// </param>
    /// <param name="newLeft">
    /// Desired new left edge of the union box (move only).
    /// </param>
    /// <param name="newTop">
    /// Desired new top edge of the union box (move only).
    /// </param>
    /// <param name="newWidth">Desired new width (resize only).</param>
    /// <param name="newHeight">Desired new height (resize only).</param>
    public void ApplyTransform(
        TransformAnchor anchor,
        double newLeft,
        double newTop,
        double newWidth,
        double newHeight)
    {
        var items = CollectTransformables();
        if (items.Count == 0) return;

        var mode = _modeService.CurrentMode;

        if (anchor == TransformAnchor.None)
        {
            // Move: distribute the same delta to every selected item.
            double dx = newLeft - Left;
            double dy = newTop  - Top;
            foreach (var item in items)
            {
                mode.ApplyTransform(
                    item,
                    TransformAnchor.None,
                    item.BoundsLeft + dx,
                    item.BoundsTop  + dy,
                    0,
                    0);
            }
        }
        else if (items.Count == 1)
        {
            // Resize: only applies to single-item selections.
            mode.ApplyTransform(items[0], anchor, 0, 0, newWidth, newHeight);
        }
    }
}
