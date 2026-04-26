using Avalonia;
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
}
