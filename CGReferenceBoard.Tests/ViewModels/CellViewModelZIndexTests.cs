using CGReferenceBoard.Layers.Abstractions;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.ViewModels;

public sealed class CellViewModelZIndexTests
{
    [Fact]
    public void ZIndex_SelectedItemCellRendersAboveUnselectedItemCell()
    {
        var unselected = new CellViewModel { Type = CellType.Image };
        var selected = new CellViewModel { Type = CellType.Image, IsSelected = true };

        Assert.True(selected.ZIndex > unselected.ZIndex);
    }

    [Fact]
    public void ZIndex_DeselectedItemCellReturnsToBaseLayer()
    {
        var cell = new CellViewModel { Type = CellType.Image, IsSelected = true };
        Assert.NotEqual(LayerZOrder.Items, cell.ZIndex);

        cell.IsSelected = false;

        Assert.Equal(LayerZOrder.Items, cell.ZIndex);
    }

    [Fact]
    public void ZIndex_DraggingItemCellKeepsExistingDraggingLayer()
    {
        var cell = new CellViewModel
        {
            Type = CellType.Image,
            IsSelected = true,
            IsDragging = true
        };

        Assert.Equal(LayerZOrder.ItemDragging, cell.ZIndex);
    }

    [Theory]
    [InlineData(CellType.Backdrop)]
    [InlineData(CellType.Image)]
    [InlineData(CellType.Label)]
    public void ZIndex_SelectedOrDraggingCellsStayBelowAnnotations(CellType type)
    {
        var selected = new CellViewModel { Type = type, IsSelected = true };
        var dragging = new CellViewModel { Type = type, IsDragging = true };

        Assert.True(selected.ZIndex < LayerZOrder.Annotations);
        Assert.True(dragging.ZIndex < LayerZOrder.Annotations);
    }
}
