using System.Collections;
using System.Linq;
using System.Reflection;
using CGReferenceBoard.Models;
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

    private static object? GetPrivateField(object instance, string fieldName)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static void SetPrivateField(object instance, string fieldName, object? value)
        => instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
}
