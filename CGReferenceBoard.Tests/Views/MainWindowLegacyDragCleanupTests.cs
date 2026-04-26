using System.Collections;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    private static object? GetPrivateField(object instance, string fieldName)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static void SetPrivateField(object instance, string fieldName, object? value)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
}
