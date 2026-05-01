using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
    #region Annotation Interaction

    private void EraseIntersectingAnnotations(Point pt)
    {
        if (Vm.EraseAnnotationsAt(pt))
            UpdateSelectionState();
    }

    private void Annotation_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm.IsViewMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Grid mode: handle Ctrl+click to deselect selected annotations
        if (!Vm.IsDrawMode && sender is Control { DataContext: AnnotationViewModel annGrid })
        {
            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            
            if (isCtrl && annGrid.IsSelected)
            {
                Vm.SelectionService.RemoveFromSelection(annGrid);
                UpdateSelectionState();
                e.Handled = true;
                return;
            }
            
            if (!annGrid.IsSelected)
                return;

            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            if (mainCanvas != null)
            {
                if (StartTransformMoveFromCurrentSelection(e.GetPosition(mainCanvas)))
                {
                    e.Pointer.Capture(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
                }
            }
            e.Handled = true;
            return;
        }

        if (!Vm.IsDrawMode)
            return;

        // Move mode: select and drag annotation
        if (Vm.IsMoveMode && sender is Control { DataContext: AnnotationViewModel annMove })
        {
            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool isAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            // Alt+Drag: Duplicate annotation and start dragging the clone
            if (isAlt)
            {
                var duplicate = new AnnotationViewModel
                {
                    CanvasX = annMove.CanvasX,
                    CanvasY = annMove.CanvasY,
                    Color = annMove.Color,
                    Thickness = annMove.Thickness,
                    TextScale = annMove.TextScale,
                    Type = annMove.Type,
                    Text = annMove.Text,
                    IsSelected = true,
                    IsInDrawMode = annMove.IsInDrawMode
                };
                foreach (var pt in annMove.Points)
                    duplicate.Points.Add(pt);
                duplicate.UpdateBoundsCache();

                Vm.Annotations.Add(duplicate);

                ClearSelection();
                Vm.SelectionService.SelectAnnotation(duplicate);
                UpdateSelectionState();
                _isAltDuplicateDrag = true;
                _pendingAltDuplicateAnnotation = duplicate;

                BringToFront(Vm.SelectionService.SelectedAnnotations);

                var canvas = _cachedMainCanvas ?? this.FindControl<Canvas>("MainCanvas");
                if (canvas != null && StartTransformMoveFromCurrentSelection(e.GetPosition(canvas)))
                {
                    e.Pointer.Capture(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
                }

                e.Handled = true;
                return;
            }

            if (isCtrl)
            {
                if (annMove.IsSelected)
                    Vm.SelectionService.RemoveFromSelection(annMove);
                else
                    Vm.SelectionService.SelectAnnotation(annMove, additive: true);
                UpdateSelectionState();
                e.Handled = true;
                return;
            }
            else if (!Vm.SelectionService.SelectedAnnotations.Contains(annMove))
            {
                ClearSelection();
                Vm.SelectionService.SelectAnnotation(annMove);
            }

            BringToFront(Vm.SelectionService.SelectedAnnotations);

            var mainCanvas = this.FindControl<Canvas>("MainCanvas");
            if (mainCanvas != null)
            {
                if (StartTransformMoveFromCurrentSelection(e.GetPosition(mainCanvas)))
                {
                    e.Pointer.Capture(_cachedCanvasBorder ?? this.FindControl<Border>("CanvasBorder"));
                }
            }
            e.Handled = true;
            return;
        }

        // Eraser mode: delete clicked annotation
        if (Vm.IsEraserMode && sender is Control { DataContext: AnnotationViewModel ann })
        {
            Vm.SelectionService.RemoveFromSelection(ann);
            Vm.Annotations.Remove(ann);
            UpdateSelectionState();
            Vm.MarkUnsaved();
            e.Handled = true;
            return;
        }

        // Text tool: edit existing text annotation
        if (Vm.CurrentTool == "Text"
            && sender is Control { DataContext: AnnotationViewModel { Type: "Text" } annText })
        {
            _editingTextAnnotation = annText;
            _editingTextAnnotationOriginalText = annText.Text;
            var editor = this.FindControl<TextBox>("AnnotationTextEditor");
            if (editor != null)
            {
                editor.Text = annText.Text;
                Canvas.SetLeft(editor, annText.Points[0].X + annText.CanvasX);
                Canvas.SetTop(editor, annText.Points[0].Y + annText.CanvasY);
                editor.IsVisible = true;
                editor.Focus();

                editor.TextChanged -= AnnotationTextEditor_TextChanged;
                editor.TextChanged += AnnotationTextEditor_TextChanged;
                editor.LostFocus -= AnnotationTextEditor_LostFocus;
                editor.LostFocus += AnnotationTextEditor_LostFocus;
                editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
                editor.AddHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown, RoutingStrategies.Tunnel);
            }
            e.Handled = true;
        }
    }

    private void AnnotationTextEditor_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_editingTextAnnotation != null && sender is TextBox editor)
            _editingTextAnnotation.Text = editor.Text ?? "";
    }

    private void AnnotationTextEditor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelTextAnnotationEditing();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            CommitTextAnnotationEditing();
            e.Handled = true;
        }
    }

    private void CancelTextAnnotationEditing()
    {
        if (_editingTextAnnotation == null)
            return;

        var editor = TryFindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (_editingTextAnnotationOriginalText == null)
            Vm.Annotations.Remove(_editingTextAnnotation);
        else
            _editingTextAnnotation.Text = _editingTextAnnotationOriginalText;

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;

        TryFindControl<Border>("CanvasBorder")?.Focus();
    }

    private void CommitTextAnnotationEditing()
    {
        if (_editingTextAnnotation == null)
            return;

        var annotation = _editingTextAnnotation;

        var editor = TryFindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (string.IsNullOrWhiteSpace(annotation.Text))
        {
            Vm.Annotations.Remove(annotation);
            Vm.SelectionService.RemoveFromSelection(annotation);
            UpdateSelectionState();
        }

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;
        Vm.MarkUnsaved();

        TryFindControl<Border>("CanvasBorder")?.Focus();
    }

    private T? TryFindControl<T>(string name) where T : Control
    {
        try
        {
            return this.FindControl<T>(name);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private void AnnotationTextEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitTextAnnotationEditing();
    }

    #endregion

    #region Annotation Context Menu & Effect

    /// <summary>
    /// Unified delete handler used by annotation context menu.
    /// Deletes all selected cells and annotations. If nothing is selected,
    /// deletes the annotation that was right-clicked.
    /// </summary>
    private void DeleteSelection_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.IsViewMode)
            return;

        bool anyDeleted = false;

        // Delete selected cells
        var selectedCells = Vm.SelectionService.SelectedCells.ToList();
        if (selectedCells.Count > 0)
        {
            foreach (var cell in selectedCells)
            {
                cell.Clear();
                Vm.GridCells.Remove(cell);
            }
            Vm.SelectionService.ClearSelection();
            _hoveredCell = null;
            anyDeleted = true;
        }

        // Delete selected annotations
        var selectedAnnotations = Vm.SelectionService.SelectedAnnotations.ToList();
        if (selectedAnnotations.Count > 0)
        {
            foreach (var ann in selectedAnnotations)
                Vm.Annotations.Remove(ann);
            Vm.SelectionService.ClearSelection();
            anyDeleted = true;
        }

        // If nothing was selected, delete the right-clicked annotation
        if (!anyDeleted && sender is MenuItem { DataContext: AnnotationViewModel clickedAnn })
        {
            Vm.Annotations.Remove(clickedAnn);
            anyDeleted = true;
        }

        if (anyDeleted)
        {
            UpdateSelectionState();
            Vm.MarkUnsaved();
            _ = ShowToastAsync("🗑 Deleted");
        }
    }

    private void AnnotationEffectNone_Click(object? sender, RoutedEventArgs e)
        => Vm.AnnotationEffectMode = "None";

    private void AnnotationEffectShadow_Click(object? sender, RoutedEventArgs e)
        => Vm.AnnotationEffectMode = "Shadow";

    private void AnnotationEffectOutline_Click(object? sender, RoutedEventArgs e)
        => Vm.AnnotationEffectMode = "Outline";

    private void GridBackgroundDots_Click(object? sender, RoutedEventArgs e)
        => Vm.GridBackgroundMode = "Dots";

    private void GridBackgroundGrid_Click(object? sender, RoutedEventArgs e)
        => Vm.GridBackgroundMode = "Grid";

    private void GridBackgroundNone_Click(object? sender, RoutedEventArgs e)
        => Vm.GridBackgroundMode = "None";

    #endregion

    #region Helper Methods

    /// <summary>
    /// Brings the specified annotations to the front of the rendering order
    /// by moving them to the end of the Vm.Annotations collection.
    /// </summary>
    private void BringToFront(IEnumerable<AnnotationViewModel> annotations)
    {
        foreach (var a in annotations.ToList())
        {
            Vm.Annotations.Remove(a);
            Vm.Annotations.Add(a);
        }
    }

    #endregion
}
