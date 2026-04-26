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
    public void CreateExpandedMoveSnapshots_SelectedBackdropIncludesOverlappingCellsAndTheirAnnotations()
    {
        var backdrop = new CellViewModel { CanvasX = 160, CanvasY = 160, ColSpan = 2, RowSpan = 2, Type = CellType.Backdrop };
        var overlappingCell = new CellViewModel { CanvasX = 160, CanvasY = 160, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var outsideCell = new CellViewModel { CanvasX = 800, CanvasY = 800, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var attachedAnnotation = new AnnotationViewModel { CanvasX = 180, CanvasY = 180, Type = "Brush" };
        attachedAnnotation.Points.Add(new Point(0, 0));
        attachedAnnotation.Points.Add(new Point(20, 20));
        attachedAnnotation.UpdateBoundsCache();
        var outsideAnnotation = new AnnotationViewModel { CanvasX = 900, CanvasY = 900, Type = "Brush" };
        outsideAnnotation.Points.Add(new Point(0, 0));
        outsideAnnotation.UpdateBoundsCache();

        var snapshots = GridTransformService.CreateExpandedMoveSnapshots(
            new[] { backdrop },
            Array.Empty<AnnotationViewModel>(),
            new[] { backdrop, overlappingCell, outsideCell },
            new[] { attachedAnnotation, outsideAnnotation });

        Assert.Contains(snapshots, snapshot => ReferenceEquals(snapshot.Cell, backdrop));
        Assert.Contains(snapshots, snapshot => ReferenceEquals(snapshot.Cell, overlappingCell));
        Assert.DoesNotContain(snapshots, snapshot => ReferenceEquals(snapshot.Cell, outsideCell));
        Assert.Contains(snapshots, snapshot => ReferenceEquals(snapshot.Annotation, attachedAnnotation));
        Assert.DoesNotContain(snapshots, snapshot => ReferenceEquals(snapshot.Annotation, outsideAnnotation));
    }

    [Fact]
    public void CreateExpandedMoveSnapshots_SelectedCellIncludesAttachedAnnotations()
    {
        var cell = new CellViewModel { CanvasX = 320, CanvasY = 480, ColSpan = 1, RowSpan = 1, Type = CellType.Image };
        var attachedAnnotation = new AnnotationViewModel { CanvasX = 340, CanvasY = 500, Type = "Brush" };
        attachedAnnotation.Points.Add(new Point(0, 0));
        attachedAnnotation.Points.Add(new Point(30, 10));
        attachedAnnotation.UpdateBoundsCache();
        var outsideAnnotation = new AnnotationViewModel { CanvasX = 640, CanvasY = 640, Type = "Brush" };
        outsideAnnotation.Points.Add(new Point(0, 0));
        outsideAnnotation.UpdateBoundsCache();

        var snapshots = GridTransformService.CreateExpandedMoveSnapshots(
            new[] { cell },
            Array.Empty<AnnotationViewModel>(),
            new[] { cell },
            new[] { attachedAnnotation, outsideAnnotation });

        Assert.Contains(snapshots, snapshot => ReferenceEquals(snapshot.Cell, cell));
        Assert.Contains(snapshots, snapshot => ReferenceEquals(snapshot.Annotation, attachedAnnotation));
        Assert.DoesNotContain(snapshots, snapshot => ReferenceEquals(snapshot.Annotation, outsideAnnotation));
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
        var resizedBounds = new Rect(0, 0, 2000, 2000);

        GridTransformService.ApplyResize(snapshots, mixedSelectionBounds, resizedBounds);

        Assert.Equal(320, cell.CanvasX);
        Assert.Equal(320, cell.CanvasY);
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
