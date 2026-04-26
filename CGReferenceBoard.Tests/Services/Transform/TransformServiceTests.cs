using Avalonia;
using CGReferenceBoard.Modes;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformServiceTests
{
    static TransformServiceTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

    [Fact]
    public void UpdatePreview_GridResizeSnapsPreviewBoundsToGrid()
    {
        var selection = CreateSelectionWithCell();
        var modeService = new ModeService();
        modeService.SetMode("Grid");
        var service = new TransformService();
        service.Refresh(selection, modeService, isViewMode: false);
        service.BeginResize(TransformHandle.BottomRight, new Point(100, 200), selection);

        service.UpdatePreview(new Point(270, 279), annotationMode: false);

        Assert.Equal(new Rect(0, 0, 320, 160), service.Bounds);
    }

    [Fact]
    public void UpdatePreview_AnnotationResizeKeepsSmoothBounds()
    {
        var selection = CreateSelectionWithCell();
        var modeService = new ModeService();
        modeService.AnnotationMode.CurrentTool = "Move";
        var service = new TransformService();
        service.Refresh(selection, modeService, isViewMode: false);
        service.BeginResize(TransformHandle.BottomRight, new Point(100, 200), selection);

        service.UpdatePreview(new Point(270, 279), annotationMode: true);

        Assert.Equal(new Rect(0, 0, 330, 239), service.Bounds);
    }

    private static SelectionService CreateSelectionWithCell()
    {
        var selection = new SelectionService();
        var cell = new CellViewModel
        {
            CanvasX = 0,
            CanvasY = 0,
            ColSpan = 1,
            RowSpan = 1,
            Type = CellType.Image
        };

        selection.SelectCell(cell);
        return selection;
    }
}
