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
        var toRemove = Vm.Annotations.Where(ann =>
        {
            double threshold = Math.Max(15, ann.Thickness / 2 + 5);
            if (ann.Points.Count == 0)
                return false;
            if (ann.Type == "Rectangle" || ann.Type == "Ellipse" || ann.Type == "Text")
            {
                var pStart = new Point(ann.Points[0].X + ann.CanvasX, ann.Points[0].Y + ann.CanvasY);
                var pEnd = new Point(ann.Points[^1].X + ann.CanvasX, ann.Points[^1].Y + ann.CanvasY);
                double left = Math.Min(pStart.X, pEnd.X);
                double right = Math.Max(pStart.X, pEnd.X);
                double top = Math.Min(pStart.Y, pEnd.Y);
                double bottom = Math.Max(pStart.Y, pEnd.Y);

                if (ann.Type == "Text")
                {
                    var renderedBounds = Helpers.AnnotationBoundsHelper.GetRenderedBounds(ann);
                    left = renderedBounds.X;
                    top = renderedBounds.Y;
                    right = renderedBounds.Right;
                    bottom = renderedBounds.Bottom;
                }

                return pt.X >= left - threshold && pt.X <= right + threshold &&
                       pt.Y >= top - threshold && pt.Y <= bottom + threshold;
            }

            if (ann.Points.Count == 1)
            {
                var p0 = new Point(ann.Points[0].X + ann.CanvasX, ann.Points[0].Y + ann.CanvasY);
                return Math.Sqrt(Math.Pow(p0.X - pt.X, 2) + Math.Pow(p0.Y - pt.Y, 2)) < threshold;
            }

            for (int i = 0; i < ann.Points.Count - 1; i++)
            {
                var p1 = new Point(ann.Points[i].X + ann.CanvasX, ann.Points[i].Y + ann.CanvasY);
                var p2 = new Point(ann.Points[i + 1].X + ann.CanvasX, ann.Points[i + 1].Y + ann.CanvasY);
                if (GeometryHelper.DistanceToSegment(pt, p1, p2) < threshold)
                    return true;
            }
            return false;
        }).ToList();

        if (toRemove.Count == 0)
            return;

        foreach (var ann in toRemove)
        {
            _selectedAnnotations.Remove(ann);
            Vm.SelectionService.RemoveFromSelection(ann);
            Vm.Annotations.Remove(ann);
        }

        UpdateSelectionState();
        Vm.MarkUnsaved();
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
                annGrid.IsSelected = false;
                _selectedAnnotations.Remove(annGrid);
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
                _selectedAnnotations.Add(duplicate);
                UpdateSelectionState();
                _isAltDuplicateDrag = true;
                _pendingAltDuplicateAnnotation = duplicate;

                BringToFront(_selectedAnnotations);

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
                annMove.IsSelected = !annMove.IsSelected;
                if (annMove.IsSelected)
                    _selectedAnnotations.Add(annMove);
                else
                    _selectedAnnotations.Remove(annMove);
                UpdateSelectionState();
                e.Handled = true;
                return;
            }
            else if (!_selectedAnnotations.Contains(annMove))
            {
                _selectedAnnotations.Clear();
                foreach (var a in Vm.Annotations)
                    a.IsSelected = false;
                _selectedAnnotations.Add(annMove);
                annMove.IsSelected = true;
            }

            BringToFront(_selectedAnnotations);

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
            _selectedAnnotations.Remove(ann);
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
            _selectedAnnotations.Remove(annotation);
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
        if (_selectedCells.Count > 0)
        {
            foreach (var cell in _selectedCells.ToList())
            {
                cell.Clear();
                Vm.GridCells.Remove(cell);
            }
            _selectedCells.Clear();
            _hoveredCell = null;
            anyDeleted = true;
        }

        // Delete selected annotations
        if (_selectedAnnotations.Count > 0)
        {
            foreach (var ann in _selectedAnnotations.ToList())
                Vm.Annotations.Remove(ann);
            _selectedAnnotations.Clear();
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
            ShowToastAsync("🗑 Deleted");
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
