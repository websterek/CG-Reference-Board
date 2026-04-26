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

    [Fact]
    public void ApplyResize_UsesCellOnlyBoundsWhenSelectionIncludesAnnotations()
    {
        var cell = new CellViewModel { CanvasX = 160, CanvasY = 160, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var annotation = new AnnotationViewModel { CanvasX = 0, CanvasY = 0, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(1000, 1000));
        annotation.UpdateBoundsCache();
        var snapshots = TransformBoundsCalculator.CreateSnapshots(new[] { cell }, new[] { annotation });
        var mixedSelectionBounds = new Rect(0, 0, 1000, 1000);
        var resizedBounds = new Rect(0, 0, 320, 320);

        GridTransformService.ApplyResize(snapshots, mixedSelectionBounds, resizedBounds);

        Assert.Equal(0, cell.CanvasX);
        Assert.Equal(0, cell.CanvasY);
        Assert.Equal(2, cell.ColSpan);
        Assert.Equal(2, cell.RowSpan);
    }

    [Fact]
    public void ApplyResize_ScalesMultipleCellsRelativeToCellSelectionBounds()
    {
        var leftCell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var rightCell = new CellViewModel { CanvasX = 160, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var snapshots = TransformBoundsCalculator.CreateSnapshots(new[] { leftCell, rightCell }, Array.Empty<AnnotationViewModel>());
        var originalBounds = new Rect(0, 0, 320, 160);
        var resizedBounds = new Rect(0, 0, 640, 320);

        GridTransformService.ApplyResize(snapshots, originalBounds, resizedBounds);

        Assert.Equal(0, leftCell.CanvasX);
        Assert.Equal(0, leftCell.CanvasY);
        Assert.Equal(2, leftCell.ColSpan);
        Assert.Equal(2, leftCell.RowSpan);
        Assert.Equal(320, rightCell.CanvasX);
        Assert.Equal(0, rightCell.CanvasY);
        Assert.Equal(2, rightCell.ColSpan);
        Assert.Equal(2, rightCell.RowSpan);
    }

    [Fact]
    public void ApplyResize_RepeatedCallsWithSameSnapshotsRemainStable()
    {
        var leftCell = new CellViewModel { CanvasX = 0, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var rightCell = new CellViewModel { CanvasX = 160, CanvasY = 0, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var snapshots = TransformBoundsCalculator.CreateSnapshots(new[] { leftCell, rightCell }, Array.Empty<AnnotationViewModel>());
        var originalBounds = new Rect(0, 0, 320, 160);
        var resizedBounds = new Rect(0, 0, 640, 320);

        GridTransformService.ApplyResize(snapshots, originalBounds, resizedBounds);
        GridTransformService.ApplyResize(snapshots, originalBounds, resizedBounds);

        Assert.Equal(0, leftCell.CanvasX);
        Assert.Equal(0, leftCell.CanvasY);
        Assert.Equal(2, leftCell.ColSpan);
        Assert.Equal(2, leftCell.RowSpan);
        Assert.Equal(320, rightCell.CanvasX);
        Assert.Equal(0, rightCell.CanvasY);
        Assert.Equal(2, rightCell.ColSpan);
        Assert.Equal(2, rightCell.RowSpan);
    }
}
