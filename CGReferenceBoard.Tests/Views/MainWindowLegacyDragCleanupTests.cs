using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using CGReferenceBoard.Views;
using Xunit;

namespace CGReferenceBoard.Tests.Views;

public sealed class MainWindowLegacyDragCleanupTests
{
    static MainWindowLegacyDragCleanupTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

    [Fact]
    public void CancelActiveInteractionForContextChange_CancelsActiveTransformAndRestoresMovedCell()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var cell = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image,
            IsSelected = true
        };

        window.Vm.GridCells.Add(cell);
        GetSelectedCells(window).Add(cell);
        window.UpdateSelectionState();
        window.Vm.TransformService.BeginMove(new Point(Constants.GridSize, Constants.GridSize), window.Vm.SelectionService);

        InvokePrivateMethod(window, "UpdateActiveTransform", new Point(Constants.GridSize * 2, Constants.GridSize * 2));

        Assert.True(window.Vm.TransformService.HasActiveOperation);
        Assert.Equal(Constants.GridSize * 2, cell.CanvasX);
        Assert.Equal(Constants.GridSize * 2, cell.CanvasY);

        InvokePrivateMethod(window, "CancelActiveInteractionForContextChange");

        Assert.False(window.Vm.TransformService.HasActiveOperation);
        Assert.Equal(Constants.GridSize, cell.CanvasX);
        Assert.Equal(Constants.GridSize, cell.CanvasY);
    }

    [Fact]
    public void CancelActiveInteractionForContextChange_CancelsActiveTransformAndRestoresMovedAnnotation()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var annotation = new AnnotationViewModel
        {
            CanvasX = 120,
            CanvasY = 80,
            Type = "Brush",
            IsSelected = true,
            IsInDrawMode = true
        };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(20, 20));
        annotation.UpdateBoundsCache();

        window.Vm.ModeService.SetMode("Annotation");
        window.Vm.ModeService.AnnotationMode.CurrentTool = "Move";
        window.Vm.Annotations.Add(annotation);
        GetSelectedAnnotations(window).Add(annotation);
        window.UpdateSelectionState();
        window.Vm.RefreshTransformState();
        window.Vm.TransformService.BeginMove(
            new Point(120, 80),
            window.Vm.SelectionService,
            new[]
            {
                TransformItemSnapshot.FromAnnotation(
                    annotation,
                    new Rect(
                        annotation.AbsBoundsLeft,
                        annotation.AbsBoundsTop,
                        annotation.AbsBoundsRight - annotation.AbsBoundsLeft,
                        annotation.AbsBoundsBottom - annotation.AbsBoundsTop))
            });

        InvokePrivateMethod(window, "UpdateActiveTransform", new Point(200, 160));

        Assert.True(window.Vm.TransformService.HasActiveOperation);
        Assert.Equal(200, annotation.CanvasX);
        Assert.Equal(160, annotation.CanvasY);

        InvokePrivateMethod(window, "CancelActiveInteractionForContextChange");

        Assert.False(window.Vm.TransformService.HasActiveOperation);
        Assert.Equal(120, annotation.CanvasX);
        Assert.Equal(80, annotation.CanvasY);
    }

    [Fact]
    public void CancelLegacyAltDuplicateDrag_RemovesPendingDuplicateAndClearsFlags()
    {
        var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        SetPrivateField(window, "<Vm>k__BackingField", new MainWindowViewModel());
        SetPrivateField(window, "_selectedCells", new System.Collections.Generic.List<CellViewModel>());
        var duplicate = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 160,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image,
            IsDragging = true,
            IsDragInvalid = true,
            IsSelected = true
        };

        window.Vm.GridCells.Add(duplicate);
        SetPrivateField(window, "_draggingCell", duplicate);
        SetPrivateField(window, "_isDraggingCell", true);
        SetPrivateField(window, "_isAltDuplicateDrag", true);

        var selectedCells = (IList)GetPrivateField(window, "_selectedCells")!;
        selectedCells.Add(duplicate);

        var cancelled = window.CancelLegacyAltDuplicateDrag();

        Assert.True(cancelled);
        Assert.DoesNotContain(duplicate, window.Vm.GridCells);
        Assert.DoesNotContain(duplicate, selectedCells.Cast<object>());
        Assert.False(duplicate.IsDragging);
        Assert.False(duplicate.IsDragInvalid);
        Assert.False((bool)GetPrivateField(window, "_isDraggingCell")!);
        Assert.False((bool)GetPrivateField(window, "_isAltDuplicateDrag")!);
        Assert.Null(GetPrivateField(window, "_draggingCell"));
    }

    [Fact]
    public void CancelPendingAnnotationAltDuplicateDrag_RemovesPendingDuplicateAndClearsTransformState()
    {
        var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var viewModel = new MainWindowViewModel();
        var duplicate = new AnnotationViewModel
        {
            CanvasX = 120,
            CanvasY = 80,
            Type = "Brush",
            IsSelected = true,
            IsInDrawMode = true
        };
        duplicate.Points.Add(new Point(0, 0));
        duplicate.Points.Add(new Point(20, 20));
        duplicate.UpdateBoundsCache();

        SetPrivateField(window, "<Vm>k__BackingField", viewModel);
        SetPrivateField(window, "_selectedCells", new System.Collections.Generic.List<CellViewModel>());
        SetPrivateField(window, "_selectedAnnotations", new System.Collections.Generic.List<AnnotationViewModel> { duplicate });

        viewModel.Annotations.Add(duplicate);
        viewModel.ModeService.SetMode("Annotation");
        viewModel.ModeService.AnnotationMode.CurrentTool = "Move";
        viewModel.SelectionService.SelectAnnotation(duplicate);
        viewModel.TransformService.BeginMove(
            new Point(120, 80),
            viewModel.SelectionService,
            new[] { TransformItemSnapshot.FromAnnotation(duplicate, new Rect(duplicate.AbsBoundsLeft, duplicate.AbsBoundsTop, duplicate.AbsBoundsRight - duplicate.AbsBoundsLeft, duplicate.AbsBoundsBottom - duplicate.AbsBoundsTop)) });

        SetPrivateField(window, "_isAltDuplicateDrag", true);
        SetPrivateField(window, "_pendingAltDuplicateAnnotation", duplicate);
        SetPrivateField(window, "_cachedTransformOverlay", new Canvas());
        SetPrivateField(window, "_cachedTransformBody", new Border());
        SetPrivateField(window, "_scale", new ScaleTransform(1, 1));

        var cancelled = window.CancelPendingAnnotationAltDuplicateDrag();

        Assert.True(cancelled);
        Assert.DoesNotContain(duplicate, viewModel.Annotations);
        Assert.DoesNotContain(duplicate, viewModel.SelectionService.SelectedAnnotations);
        Assert.DoesNotContain(duplicate, ((IList)GetPrivateField(window, "_selectedAnnotations")!).Cast<object>());
        Assert.False((bool)GetPrivateField(window, "_isAltDuplicateDrag")!);
        Assert.Null(GetPrivateField(window, "_pendingAltDuplicateAnnotation"));
        Assert.False(viewModel.TransformService.HasActiveOperation);
        Assert.False(viewModel.TransformService.IsVisible);
        Assert.Empty(viewModel.TransformService.ActiveSnapshots);
    }

    [Fact]
    public void HandleEscapeShortcut_CancelsLegacyAltDuplicateDrag()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var duplicate = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image,
            IsDragging = true,
            IsDragInvalid = true,
            IsSelected = true
        };

        window.Vm.GridCells.Add(duplicate);
        GetSelectedCells(window).Add(duplicate);
        SetPrivateField(window, "_draggingCell", duplicate);
        SetPrivateField(window, "_isDraggingCell", true);
        SetPrivateField(window, "_isAltDuplicateDrag", true);

        var handled = (bool)InvokePrivateMethod(window, "HandleEscapeShortcut")!;

        Assert.True(handled);
        Assert.DoesNotContain(duplicate, window.Vm.GridCells);
        Assert.DoesNotContain(duplicate, GetSelectedCells(window));
        Assert.False(duplicate.IsDragging);
        Assert.False(duplicate.IsDragInvalid);
        Assert.False((bool)GetPrivateField(window, "_isDraggingCell")!);
        Assert.False((bool)GetPrivateField(window, "_isAltDuplicateDrag")!);
        Assert.Null(GetPrivateField(window, "_draggingCell"));
    }

    [Fact]
    public void ClearPendingAnnotationAltDuplicateState_ClearsAnnotationDuplicateCancelFlags()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var duplicate = new AnnotationViewModel
        {
            CanvasX = 160,
            CanvasY = 120,
            Type = "Brush",
            IsSelected = true,
            IsInDrawMode = true
        };
        duplicate.Points.Add(new Point(0, 0));
        duplicate.Points.Add(new Point(20, 20));
        duplicate.UpdateBoundsCache();

        window.Vm.ModeService.SetMode("Annotation");
        window.Vm.ModeService.AnnotationMode.CurrentTool = "Move";
        window.Vm.Annotations.Add(duplicate);
        GetSelectedAnnotations(window).Add(duplicate);
        window.Vm.SelectionService.SelectAnnotation(duplicate);

        SetPrivateField(window, "_isAltDuplicateDrag", true);
        SetPrivateField(window, "_pendingAltDuplicateAnnotation", duplicate);

        InvokePrivateMethod(window, "ClearPendingAnnotationAltDuplicateState");

        Assert.Contains(duplicate, window.Vm.Annotations);
        Assert.Contains(duplicate, window.Vm.SelectionService.SelectedAnnotations);
        Assert.Contains(duplicate, GetSelectedAnnotations(window));
        Assert.False((bool)GetPrivateField(window, "_isAltDuplicateDrag")!);
        Assert.Null(GetPrivateField(window, "_pendingAltDuplicateAnnotation"));
    }

    [Fact]
    public void HandleDeleteSelection_AnnotationOnlySelection_RefreshesSharedSelectionState()
    {
        var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var viewModel = new MainWindowViewModel();
        var annotation = new AnnotationViewModel
        {
            CanvasX = 120,
            CanvasY = 80,
            Type = "Brush",
            IsSelected = true
        };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(20, 20));
        annotation.UpdateBoundsCache();

        SetPrivateField(window, "<Vm>k__BackingField", viewModel);
        SetPrivateField(window, "_selectedCells", new System.Collections.Generic.List<CellViewModel>());
        SetPrivateField(window, "_selectedAnnotations", new System.Collections.Generic.List<AnnotationViewModel> { annotation });
        SetPrivateField(window, "_cachedTransformOverlay", new Canvas());
        SetPrivateField(window, "_cachedTransformBody", new Border());
        SetPrivateField(window, "_scale", new ScaleTransform(1, 1));

        viewModel.Annotations.Add(annotation);
        viewModel.ModeService.SetMode("Annotation");
        viewModel.ModeService.AnnotationMode.CurrentTool = "Move";
        viewModel.SelectionService.SelectAnnotation(annotation);
        viewModel.TransformService.Refresh(viewModel.SelectionService, viewModel.ModeService, viewModel.IsViewMode);

        var deleted = (bool)typeof(MainWindow)
            .GetMethod("DeleteSelectedContent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, new object?[] { false })!;

        Assert.True(deleted);
        Assert.Empty(viewModel.Annotations);
        Assert.Empty(viewModel.SelectionService.SelectedAnnotations);
        Assert.False(viewModel.SelectionService.HasSelection);
        Assert.False(viewModel.TransformService.IsVisible);
        Assert.Equal(TransformCapabilities.None, viewModel.TransformService.Capabilities);
    }

    [Fact]
    public void CommitTextAnnotationEditing_EmptyText_RemovesDeletedAnnotationFromSelectionState()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var annotation = new AnnotationViewModel
        {
            CanvasX = 120,
            CanvasY = 80,
            Type = "Text",
            Text = "Delete me",
            IsSelected = true,
            IsInDrawMode = true
        };
        annotation.Points.Add(new Point(10, 20));
        annotation.UpdateBoundsCache();

        window.Vm.ModeService.SetMode("Annotation");
        window.Vm.ModeService.AnnotationMode.CurrentTool = "Move";
        window.Vm.Annotations.Add(annotation);
        GetSelectedAnnotations(window).Add(annotation);
        window.Vm.SelectionService.SelectAnnotation(annotation);
        window.Vm.TransformService.Refresh(window.Vm.SelectionService, window.Vm.ModeService, window.Vm.IsViewMode);

        annotation.Text = "";
        SetPrivateField(window, "_editingTextAnnotation", annotation);
        SetPrivateField(window, "_editingTextAnnotationOriginalText", "Delete me");

        InvokePrivateMethod(window, "CommitTextAnnotationEditing");

        Assert.DoesNotContain(annotation, window.Vm.Annotations);
        Assert.DoesNotContain(annotation, GetSelectedAnnotations(window));
        Assert.DoesNotContain(annotation, window.Vm.SelectionService.SelectedAnnotations);
        Assert.False(window.Vm.SelectionService.HasSelection);
        Assert.False(window.Vm.TransformService.IsVisible);
        Assert.Equal(TransformCapabilities.None, window.Vm.TransformService.Capabilities);
    }

    [Fact]
    public void SelectContent_Click_UsesRenderedBackdropExtents()
    {
        var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        var viewModel = new MainWindowViewModel();
        var backdrop = new CellViewModel
        {
            CanvasX = 320,
            CanvasY = 320,
            ColSpan = 2,
            RowSpan = 2,
            Type = CellType.Backdrop
        };
        var cellInBackdropPadding = new CellViewModel
        {
            CanvasX = backdrop.VisualX + 10,
            CanvasY = backdrop.VisualY + 10,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };
        cellInBackdropPadding.SetText("inside rendered backdrop padding");

        SetPrivateField(window, "<Vm>k__BackingField", viewModel);
        SetPrivateField(window, "_selectedCells", new System.Collections.Generic.List<CellViewModel>());
        SetPrivateField(window, "_selectedAnnotations", new System.Collections.Generic.List<AnnotationViewModel>());
        SetPrivateField(window, "_cachedTransformOverlay", new Canvas());
        SetPrivateField(window, "_cachedTransformBody", new Border());
        SetPrivateField(window, "_scale", new ScaleTransform(1, 1));

        viewModel.GridCells.Add(backdrop);
        viewModel.GridCells.Add(cellInBackdropPadding);

        var menuItem = new MenuItem { DataContext = backdrop };

        typeof(MainWindow)
            .GetMethod("SelectContent_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, new object?[] { menuItem, null! });

        Assert.Contains(cellInBackdropPadding, viewModel.SelectionService.SelectedCells);
        Assert.True(cellInBackdropPadding.IsSelected);
    }

    [Fact]
    public void MainWindow_TryStartTransformBodyMove_DoesNotStartMoveForNonLeftPress()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var cell = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 2,
            RowSpan = 1,
            Type = CellType.Image,
            IsSelected = true
        };

        window.Vm.GridCells.Add(cell);
        GetSelectedCells(window).Add(cell);
        window.UpdateSelectionState();
        window.Vm.RefreshTransformState();

        var method = typeof(MainWindow).GetMethod("TryStartTransformBodyMove", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var started = (bool)method!.Invoke(window, new object?[] { new Point(Constants.GridSize + 20, Constants.GridSize + 20), false })!;

        Assert.False(started);
        Assert.False(window.Vm.TransformService.HasActiveOperation);
    }

    [Fact]
    public void MainWindow_TryStartTransformBodyMove_StartsMoveForPointerInsideBody()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var cell = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 2,
            RowSpan = 1,
            Type = CellType.Image,
            IsSelected = true
        };

        window.Vm.GridCells.Add(cell);
        GetSelectedCells(window).Add(cell);
        window.UpdateSelectionState();
        window.Vm.RefreshTransformState();

        var method = typeof(MainWindow).GetMethod("TryStartTransformBodyMove", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var started = (bool)method!.Invoke(window, new object?[] { new Point(Constants.GridSize + 20, Constants.GridSize + 20), true })!;

        Assert.True(started);
        Assert.True(window.Vm.TransformService.HasActiveOperation);
        Assert.Equal(TransformOperation.Move, window.Vm.TransformService.Operation);
    }

    [Fact]
    public void MainWindow_TryStartTransformBodyMove_MarksSelectedCellsAsDraggingUntilTransformEnds()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var cell = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 2,
            RowSpan = 1,
            Type = CellType.Image,
            IsSelected = true
        };

        window.Vm.GridCells.Add(cell);
        GetSelectedCells(window).Add(cell);
        window.UpdateSelectionState();
        window.Vm.RefreshTransformState();

        var startMethod = typeof(MainWindow).GetMethod("TryStartTransformBodyMove", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(startMethod);

        var started = (bool)startMethod!.Invoke(window, new object?[] { new Point(Constants.GridSize + 20, Constants.GridSize + 20), true })!;

        Assert.True(started);
        Assert.True(cell.IsDragging);

        var cancelled = (bool)InvokePrivateMethod(window, "CancelActiveTransform")!;

        Assert.True(cancelled);
        Assert.False(cell.IsDragging);
    }

    [Fact]
    public void MainWindow_TryStartTransformBodyMove_DoesNotStartMoveFromHandleRegion()
    {
        var window = CreateWindowHarness(new MainWindowViewModel());
        var cell = new CellViewModel
        {
            CanvasX = Constants.GridSize,
            CanvasY = Constants.GridSize,
            ColSpan = 2,
            RowSpan = 1,
            Type = CellType.Image,
            IsSelected = true
        };

        window.Vm.GridCells.Add(cell);
        GetSelectedCells(window).Add(cell);
        window.UpdateSelectionState();
        window.Vm.RefreshTransformState();

        var method = typeof(MainWindow).GetMethod("TryStartTransformBodyMove", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var started = (bool)method!.Invoke(window, new object?[] { new Point(Constants.GridSize, Constants.GridSize), true })!;

        Assert.False(started);
        Assert.False(window.Vm.TransformService.HasActiveOperation);
    }

    private static object? GetPrivateField(object instance, string fieldName)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static MainWindow CreateWindowHarness(MainWindowViewModel viewModel)
    {
        var window = (MainWindow)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        SetPrivateField(window, "<Vm>k__BackingField", viewModel);
        SetPrivateField(window, "_selectedCells", new List<CellViewModel>());
        SetPrivateField(window, "_selectedAnnotations", new List<AnnotationViewModel>());
        SetPrivateField(window, "_cachedTransformOverlay", new Canvas());
        SetPrivateField(window, "_cachedTransformBody", new Border());
        SetPrivateField(window, "_scale", new ScaleTransform(1, 1));
        return window;
    }

    private static List<CellViewModel> GetSelectedCells(MainWindow window)
        => (List<CellViewModel>)GetPrivateField(window, "_selectedCells")!;

    private static List<AnnotationViewModel> GetSelectedAnnotations(MainWindow window)
        => (List<AnnotationViewModel>)GetPrivateField(window, "_selectedAnnotations")!;

    private static void SetPrivateField(object instance, string fieldName, object? value)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[]? parameters)
        => instance.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, parameters);
}
