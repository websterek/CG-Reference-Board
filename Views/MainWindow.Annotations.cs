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

    private double DistanceToSegment(Point p, Point v, Point w)
    {
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
        if (l2 == 0)
            return Math.Sqrt(Math.Pow(p.X - v.X, 2) + Math.Pow(p.Y - v.Y, 2));
        double t = Math.Max(0, Math.Min(1, ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2));
        Point projection = new Point(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
        return Math.Sqrt(Math.Pow(p.X - projection.X, 2) + Math.Pow(p.Y - projection.Y, 2));
    }

    private void EraseIntersectingAnnotations(Point pt)
    {
        var toRemove = Annotations.Where(ann =>
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
                    right = left + 150;
                    bottom = top + 50;
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
                if (DistanceToSegment(pt, p1, p2) < threshold)
                    return true;
            }
            return false;
        }).ToList();

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

        // Grid mode: drag already-selected annotations (unselected ones are not hit-testable)
        if (!IsDrawMode && sender is Control { DataContext: AnnotationViewModel annGrid } && annGrid.IsSelected)
        {
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
            e.Handled = true;
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

            // Bring all selected annotations to front (end of collection = highest z-order)
            foreach (var a in _selectedAnnotations.ToList())
            {
                Annotations.Remove(a);
                Annotations.Add(a);
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

    #region Annotation Context Menu & Effect

    /// <summary>
    /// Unified delete handler used by annotation context menu.
    /// Deletes all selected cells and annotations. If nothing is selected,
    /// deletes the annotation that was right-clicked.
    /// </summary>
    private void DeleteSelection_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;

        bool anyDeleted = false;

        // Delete selected cells
        if (_selectedCells.Count > 0)
        {
            foreach (var cell in _selectedCells.ToList())
            {
                cell.Clear();
                GridCells.Remove(cell);
            }
            _selectedCells.Clear();
            _hoveredCell = null;
            anyDeleted = true;
        }

        // Delete selected annotations
        if (_selectedAnnotations.Count > 0)
        {
            foreach (var ann in _selectedAnnotations.ToList())
                Annotations.Remove(ann);
            _selectedAnnotations.Clear();
            anyDeleted = true;
        }

        // If nothing was selected, delete the right-clicked annotation
        if (!anyDeleted && sender is MenuItem { DataContext: AnnotationViewModel clickedAnn })
        {
            Annotations.Remove(clickedAnn);
            anyDeleted = true;
        }

        if (anyDeleted)
        {
            UpdateSelectionState();
            MarkUnsaved();
            SaveBoardData();
            ShowToast("🗑 Deleted");
        }
    }

    private void AnnotationEffectNone_Click(object? sender, RoutedEventArgs e)
        => AnnotationEffectMode = "None";

    private void AnnotationEffectShadow_Click(object? sender, RoutedEventArgs e)
        => AnnotationEffectMode = "Shadow";

    private void AnnotationEffectOutline_Click(object? sender, RoutedEventArgs e)
        => AnnotationEffectMode = "Outline";

    private void GridBackgroundDots_Click(object? sender, RoutedEventArgs e)
        => GridBackgroundMode = "Dots";

    private void GridBackgroundGrid_Click(object? sender, RoutedEventArgs e)
        => GridBackgroundMode = "Grid";

    private void GridBackgroundNone_Click(object? sender, RoutedEventArgs e)
        => GridBackgroundMode = "None";

    #endregion
}
