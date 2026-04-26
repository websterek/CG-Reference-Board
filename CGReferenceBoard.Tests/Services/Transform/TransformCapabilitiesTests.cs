using Avalonia;
using CGReferenceBoard.Modes;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformCapabilitiesTests
{
    [Fact]
    public void BeginMove_DoesNothingWhenCapabilitiesDisallowMove()
    {
        var selection = CreateSelectionWithCell();
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(false, true, false, false),
            IsVisible = true,
            Bounds = new Rect(10, 20, 160, 160)
        };

        service.BeginMove(new Point(100, 200), selection);

        Assert.False(service.HasActiveOperation);
        Assert.Equal(TransformOperation.None, service.Operation);
        Assert.Equal(TransformHandle.None, service.ActiveHandle);
        Assert.Empty(service.ActiveSnapshots);
        Assert.Equal(default, service.StartPointer);
    }

    [Fact]
    public void BeginResize_DoesNothingWhenCapabilitiesDisallowResize()
    {
        var selection = CreateSelectionWithCell();
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(true, false, false, false),
            IsVisible = true,
            Bounds = new Rect(10, 20, 160, 160)
        };

        service.BeginResize(TransformHandle.BottomRight, new Point(100, 200), selection);

        Assert.False(service.HasActiveOperation);
        Assert.Equal(TransformOperation.None, service.Operation);
        Assert.Equal(TransformHandle.None, service.ActiveHandle);
        Assert.Empty(service.ActiveSnapshots);
        Assert.Equal(default, service.StartPointer);
    }

    [Fact]
    public void BeginMove_StartsOperationWhenCapabilitiesAllowMove()
    {
        var selection = CreateSelectionWithCell();
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(true, false, false, false)
        };

        service.BeginMove(new Point(100, 200), selection);

        Assert.True(service.HasActiveOperation);
        Assert.Equal(TransformOperation.Move, service.Operation);
        Assert.Equal(TransformHandle.Body, service.ActiveHandle);
        Assert.Single(service.ActiveSnapshots);
        Assert.Equal(new Point(100, 200), service.StartPointer);
    }

    [Fact]
    public void BeginMove_UsesProvidedSnapshotsInsteadOfCurrentSelection()
    {
        var selection = new SelectionService();
        var selectedAnnotation = CreateAnnotation();
        selection.SelectAnnotation(selectedAnnotation);
        var replacementCell = new CellViewModel
        {
            CanvasX = 320,
            CanvasY = 480,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };
        var replacementSnapshots = TransformBoundsCalculator.CreateSnapshots(new[] { replacementCell }, Array.Empty<AnnotationViewModel>());
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(true, false, false, false)
        };

        service.BeginMove(new Point(25, 35), selection, replacementSnapshots);

        var snapshot = Assert.Single(service.ActiveSnapshots);
        Assert.Same(replacementCell, snapshot.Cell);
        Assert.Null(snapshot.Annotation);
        Assert.Equal(new Point(25, 35), service.StartPointer);
    }

    [Fact]
    public void BeginResize_StartsOperationWhenCapabilitiesAllowResize()
    {
        var selection = CreateSelectionWithCell();
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(false, true, false, false)
        };

        service.BeginResize(TransformHandle.BottomRight, new Point(100, 200), selection);

        Assert.True(service.HasActiveOperation);
        Assert.Equal(TransformOperation.Resize, service.Operation);
        Assert.Equal(TransformHandle.BottomRight, service.ActiveHandle);
        Assert.Single(service.ActiveSnapshots);
        Assert.Equal(new Point(100, 200), service.StartPointer);
    }

    [Fact]
    public void Cancel_RestoresIdleStateAndClearsSnapshots()
    {
        var selection = CreateSelectionWithCell();
        var service = new TransformService
        {
            Capabilities = new TransformCapabilities(true, true, false, false),
            IsVisible = true,
            Bounds = new Rect(10, 20, 160, 160)
        };
        service.BeginMove(new Point(100, 200), selection);

        service.Cancel();

        Assert.False(service.HasActiveOperation);
        Assert.Equal(TransformOperation.None, service.Operation);
        Assert.Equal(TransformHandle.None, service.ActiveHandle);
        Assert.Empty(service.ActiveSnapshots);
        Assert.Equal(default, service.StartBounds);
        Assert.Equal(default, service.StartPointer);
        Assert.True(service.IsVisible);
        Assert.Equal(new Rect(10, 20, 160, 160), service.Bounds);
    }

    [Fact]
    public void Refresh_GridModeWithAnnotationOnlySelection_DisablesResize()
    {
        var selection = new SelectionService();
        var annotation = CreateAnnotation();
        selection.SelectAnnotation(annotation);
        var modeService = new ModeService();
        modeService.SetMode("Grid");
        var service = new TransformService();

        service.Refresh(selection, modeService, isViewMode: false);

        Assert.True(service.IsVisible);
        Assert.True(service.Capabilities.CanMove);
        Assert.False(service.Capabilities.CanResize);
    }

    [Fact]
    public void Refresh_GridModeWithMixedSelection_DisablesResize()
    {
        var selection = CreateSelectionWithCell();
        selection.SelectAnnotation(CreateAnnotation(), additive: true);
        var modeService = new ModeService();
        modeService.SetMode("Grid");
        var service = new TransformService();

        service.Refresh(selection, modeService, isViewMode: false);

        Assert.True(service.IsVisible);
        Assert.True(service.Capabilities.CanMove);
        Assert.False(service.Capabilities.CanResize);
    }

    [Fact]
    public void Refresh_GridModeWithCellOnlySelection_KeepsResizeEnabled()
    {
        var selection = CreateSelectionWithCell();
        var modeService = new ModeService();
        modeService.SetMode("Grid");
        var service = new TransformService();

        service.Refresh(selection, modeService, isViewMode: false);

        Assert.True(service.IsVisible);
        Assert.True(service.Capabilities.CanMove);
        Assert.True(service.Capabilities.CanResize);
    }

    private static SelectionService CreateSelectionWithCell()
    {
        var selection = new SelectionService();
        var cell = new CellViewModel
        {
            CanvasX = 10,
            CanvasY = 20,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };

        selection.SelectCell(cell);
        return selection;
    }

    private static AnnotationViewModel CreateAnnotation()
    {
        var annotation = new AnnotationViewModel { CanvasX = 10, CanvasY = 20, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(30, 40));
        annotation.UpdateBoundsCache();
        return annotation;
    }
}
