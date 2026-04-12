using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
    #region Annotation Interaction

    private void EraseIntersectingAnnotations(Point pt)
    {
        var toRemove = Annotations.Where(ann =>
            ann.Points.Any(p =>
                Math.Abs(p.X + ann.CanvasX - pt.X) < 15
                && Math.Abs(p.Y + ann.CanvasY - pt.Y) < 15))
            .ToList();

        if (toRemove.Count == 0)
            return;

        foreach (var ann in toRemove)
            Annotations.Remove(ann);
        MarkUnsaved();
        SaveBoardData();
    }

    private void Annotation_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isViewMode || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // Grid mode: click annotation to select it and enable grid-snapped dragging
        if (!IsDrawMode && sender is Control { DataContext: AnnotationViewModel annGrid })
        {
            bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (isCtrl)
            {
                annGrid.IsSelected = !annGrid.IsSelected;
                if (annGrid.IsSelected)
                    _selectedAnnotations.Add(annGrid);
                else
                    _selectedAnnotations.Remove(annGrid);
            }
            else
            {
                if (!annGrid.IsSelected)
                {
                    ClearSelection();
                    annGrid.IsSelected = true;
                    _selectedAnnotations.Add(annGrid);
                }

                _isDraggingAnnotations = true;
                _annotationDragCellOriginals = _selectedCells.Select(c => (c, c.CanvasX, c.CanvasY)).ToList();
                foreach (var (c, _, _) in _annotationDragCellOriginals)
                    c.IsDragging = true;
                var mainCanvas = this.FindControl<Canvas>("MainCanvas");
                if (mainCanvas != null)
                {
                    _annotationDragStart = e.GetPosition(mainCanvas);
                }
                e.Pointer.Capture(sender as Control);
            }

            e.Handled = true;
            OnPropertyChanged(nameof(SelectionCountText));
            return;
        }

        if (!IsDrawMode)
            return;

        // Move mode: select and drag annotation
        if (IsMoveMode && sender is Control { DataContext: AnnotationViewModel annMove })
        {
            if (!_selectedAnnotations.Contains(annMove))
            {
                _selectedAnnotations.Clear();
                foreach (var a in Annotations)
                    a.IsSelected = false;
                _selectedAnnotations.Add(annMove);
                annMove.IsSelected = true;
            }

            _isDraggingAnnotations = true;
            _annotationDragCellOriginals = null;
            _annotationDragStart = e.GetPosition(this.FindControl<Canvas>("MainCanvas"));
            e.Handled = true;
            return;
        }

        // Eraser mode: delete clicked annotation
        if (IsEraserMode && sender is Control { DataContext: AnnotationViewModel ann })
        {
            Annotations.Remove(ann);
            MarkUnsaved();
            SaveBoardData();
            e.Handled = true;
            return;
        }

        // Text tool: edit existing text annotation
        if (CurrentTool == "Text"
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

        var editor = this.FindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (_editingTextAnnotationOriginalText == null)
            Annotations.Remove(_editingTextAnnotation);
        else
            _editingTextAnnotation.Text = _editingTextAnnotationOriginalText;

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;

        this.FindControl<Border>("CanvasBorder")?.Focus();
    }

    private void CommitTextAnnotationEditing()
    {
        if (_editingTextAnnotation == null)
            return;

        var editor = this.FindControl<TextBox>("AnnotationTextEditor");
        if (editor != null)
        {
            editor.RemoveHandler(InputElement.KeyDownEvent, AnnotationTextEditor_KeyDown);
            editor.TextChanged -= AnnotationTextEditor_TextChanged;
            editor.LostFocus -= AnnotationTextEditor_LostFocus;
            editor.IsVisible = false;
        }

        if (string.IsNullOrWhiteSpace(_editingTextAnnotation.Text))
            Annotations.Remove(_editingTextAnnotation);

        _editingTextAnnotation = null;
        _editingTextAnnotationOriginalText = null;
        MarkUnsaved();
        SaveBoardData();

        this.FindControl<Border>("CanvasBorder")?.Focus();
    }

    private void AnnotationTextEditor_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitTextAnnotationEditing();
    }

    #endregion
}
