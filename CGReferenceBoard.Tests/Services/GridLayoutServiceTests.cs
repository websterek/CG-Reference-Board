using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services;

public sealed class GridLayoutServiceTests
{
    [Fact]
    public void GetBackdropAnnotations_UsesRenderedTextBounds()
    {
        var backdrop = new CellViewModel
        {
            CanvasX = 320,
            CanvasY = 480,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Backdrop
        };
        var overlappingText = new AnnotationViewModel
        {
            CanvasX = 300,
            CanvasY = 460,
            Type = "Text",
            Text = "Backdrop child",
            TextScale = 2.5,
            Thickness = 3
        };
        overlappingText.Points.Add(new Point(0, 0));

        var attached = GridLayoutService.GetBackdropAnnotations(new[] { overlappingText }, backdrop);

        Assert.Contains(overlappingText, attached);
    }

    [Fact]
    public void GetBackdropChildren_UsesRenderedBackdropBounds()
    {
        var backdrop = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 160,
            ColSpan = 2,
            RowSpan = 2,
            Type = CellType.Backdrop
        };
        var childInPaddedBand = new CellViewModel
        {
            CanvasX = 80,
            CanvasY = 80,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };

        var children = GridLayoutService.GetBackdropChildren(new[] { backdrop, childInPaddedBand }, backdrop);

        Assert.Contains(childInPaddedBand, children);
    }

    [Fact]
    public void GetBackdropAnnotations_UsesRenderedBackdropBounds()
    {
        var backdrop = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 160,
            ColSpan = 2,
            RowSpan = 2,
            Type = CellType.Backdrop
        };
        var overlappingText = new AnnotationViewModel
        {
            CanvasX = 10,
            CanvasY = 90,
            Type = "Text",
            Text = "Backdrop child",
            TextScale = 1.5,
            Thickness = 3
        };
        overlappingText.Points.Add(new Point(0, 0));

        var attached = GridLayoutService.GetBackdropAnnotations(new[] { overlappingText }, backdrop);

        Assert.Contains(overlappingText, attached);
    }

    [Fact]
    public void MoveAnnotationsWithCells_UsesRenderedTextBounds()
    {
        var cell = new CellViewModel
        {
            CanvasX = 320,
            CanvasY = 480,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };
        var overlappingText = new AnnotationViewModel
        {
            CanvasX = 300,
            CanvasY = 460,
            Type = "Text",
            Text = "Move me",
            TextScale = 2.5,
            Thickness = 3
        };
        overlappingText.Points.Add(new Point(0, 0));

        var oldPositions = new Dictionary<CellViewModel, Point>
        {
            [cell] = new Point(cell.CanvasX, cell.CanvasY)
        };

        cell.CanvasX += 160;
        cell.CanvasY += 160;

        GridLayoutService.MoveAnnotationsWithCells(new[] { overlappingText }, oldPositions);

        Assert.Equal(460, overlappingText.CanvasX);
        Assert.Equal(620, overlappingText.CanvasY);
    }

    [Fact]
    public void MoveAnnotationsWithCells_BackdropUsesRenderedBounds()
    {
        var backdrop = new CellViewModel
        {
            CanvasX = 160,
            CanvasY = 160,
            ColSpan = 2,
            RowSpan = 2,
            Type = CellType.Backdrop
        };
        var overlappingText = new AnnotationViewModel
        {
            CanvasX = 10,
            CanvasY = 90,
            Type = "Text",
            Text = "Move me",
            TextScale = 1.5,
            Thickness = 3
        };
        overlappingText.Points.Add(new Point(0, 0));

        var oldPositions = new Dictionary<CellViewModel, Point>
        {
            [backdrop] = new Point(backdrop.CanvasX, backdrop.CanvasY)
        };

        backdrop.CanvasX += 160;
        backdrop.CanvasY += 160;

        GridLayoutService.MoveAnnotationsWithCells(new[] { overlappingText }, oldPositions);

        Assert.Equal(170, overlappingText.CanvasX);
        Assert.Equal(250, overlappingText.CanvasY);
    }
}
