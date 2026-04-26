using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class GridTransformServiceTests
{
    [Fact]
    public void ApplyMove_SnapsCellsAndMovesAnnotationsBySameDelta()
    {
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var annotation = new AnnotationViewModel { CanvasX = 5, CanvasY = 10, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        var snapshots = TransformBoundsCalculator.CreateSnapshots(new[] { cell }, new[] { annotation });

        GridTransformService.ApplyMove(snapshots, new Vector(170, 79));

        Assert.Equal(160, cell.CanvasX);
        Assert.Equal(0, cell.CanvasY);
        Assert.Equal(165, annotation.CanvasX);
        Assert.Equal(10, annotation.CanvasY);
    }

    [Fact]
    public void ApplySingleCellResize_SnapsToGridSpan()
    {
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var snapshot = TransformBoundsCalculator.CreateSnapshots(new[] { cell }, Array.Empty<AnnotationViewModel>());
        var originalBounds = new Rect(0, 0, 160, 160);
        var resizedBounds = new Rect(0, 0, 330, 500);

        GridTransformService.ApplyResize(snapshot, originalBounds, resizedBounds);

        Assert.Equal(2, cell.ColSpan);
        Assert.Equal(3, cell.RowSpan);
    }
}
