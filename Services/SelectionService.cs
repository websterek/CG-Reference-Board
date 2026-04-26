using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

/// <summary>
/// Manages the current selection of <see cref="CellViewModel"/> and
/// <see cref="AnnotationViewModel"/> items. Keeps the <c>IsSelected</c> flag
/// on each item in sync with the selection collections.
///
/// All mutation methods are safe to call from the UI thread.
/// </summary>
public sealed partial class SelectionService : ObservableObject
{
    public event EventHandler? SelectionChanged;

    // ── Selection collections ─────────────────────────────────────────────────

    /// <summary>Currently selected grid cells. Do not modify directly — use the Select/Clear methods.</summary>
    public ObservableCollection<CellViewModel> SelectedCells { get; } = new();

    /// <summary>Currently selected annotations. Do not modify directly — use the Select/Clear methods.</summary>
    public ObservableCollection<AnnotationViewModel> SelectedAnnotations { get; } = new();

    // ── Computed properties ───────────────────────────────────────────────────

    /// <summary>True when at least one item is selected.</summary>
    public bool HasSelection => SelectedCells.Count + SelectedAnnotations.Count > 0;

    /// <summary>True when more than one item is selected.</summary>
    public bool HasMultipleSelection => SelectedCells.Count + SelectedAnnotations.Count > 1;

    /// <summary>True when exactly one item is selected.</summary>
    public bool HasSingleSelection => SelectedCells.Count + SelectedAnnotations.Count == 1;

    /// <summary>Total number of selected items.</summary>
    public int SelectionCount => SelectedCells.Count + SelectedAnnotations.Count;

    /// <summary>Human-readable selection count, e.g. "3 selected", or empty string when nothing is selected.</summary>
    public string SelectionCountText => SelectionCount > 0 ? $"{SelectionCount} selected" : "";

    // ── Constructor ───────────────────────────────────────────────────────────

    public SelectionService()
    {
        SelectedCells.CollectionChanged += (_, _) => NotifySelectionChanged();
        SelectedAnnotations.CollectionChanged += (_, _) => NotifySelectionChanged();
    }

    // ── Selection mutations ───────────────────────────────────────────────────

    /// <summary>
    /// Selects a single cell. When <paramref name="additive"/> is false (default),
    /// the current selection is cleared first.
    /// </summary>
    public void SelectCell(CellViewModel cell, bool additive = false)
    {
        if (!additive) ClearSelection();

        if (!SelectedCells.Contains(cell))
        {
            cell.IsSelected = true;
            SelectedCells.Add(cell);
        }
    }

    /// <summary>
    /// Selects a single annotation. When <paramref name="additive"/> is false (default),
    /// the current selection is cleared first.
    /// </summary>
    public void SelectAnnotation(AnnotationViewModel ann, bool additive = false)
    {
        if (!additive) ClearSelection();

        if (!SelectedAnnotations.Contains(ann))
        {
            ann.IsSelected = true;
            SelectedAnnotations.Add(ann);
        }
    }

    /// <summary>
    /// Selects a range of cells and annotations. When <paramref name="additive"/> is false
    /// (default), the current selection is cleared first.
    /// </summary>
    public void SelectRange(
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations,
        bool additive = false)
    {
        if (!additive) ClearSelection();

        foreach (var cell in cells)
        {
            if (!SelectedCells.Contains(cell))
            {
                cell.IsSelected = true;
                SelectedCells.Add(cell);
            }
        }

        foreach (var ann in annotations)
        {
            if (!SelectedAnnotations.Contains(ann))
            {
                ann.IsSelected = true;
                SelectedAnnotations.Add(ann);
            }
        }
    }

    /// <summary>Clears all selected items and resets their <c>IsSelected</c> flags.</summary>
    public void ClearSelection()
    {
        foreach (var cell in SelectedCells) cell.IsSelected = false;
        foreach (var ann in SelectedAnnotations) ann.IsSelected = false;
        SelectedCells.Clear();
        SelectedAnnotations.Clear();
    }

    /// <summary>Removes a single cell from the selection.</summary>
    public void RemoveFromSelection(CellViewModel cell)
    {
        cell.IsSelected = false;
        SelectedCells.Remove(cell);
    }

    /// <summary>Removes a single annotation from the selection.</summary>
    public void RemoveFromSelection(AnnotationViewModel ann)
    {
        ann.IsSelected = false;
        SelectedAnnotations.Remove(ann);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(SelectionCountText));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
