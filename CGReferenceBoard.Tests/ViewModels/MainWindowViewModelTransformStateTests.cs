using Avalonia;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.ViewModels;

public sealed class MainWindowViewModelTransformStateTests
{
    [Fact]
    public void AnnotationToolChange_RefreshesTransformState()
    {
        var viewModel = new MainWindowViewModel();
        var annotation = new AnnotationViewModel { CanvasX = 20, CanvasY = 30, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(30, 30));
        annotation.UpdateBoundsCache();

        viewModel.ModeService.SetMode("Annotation");
        viewModel.SelectionService.SelectAnnotation(annotation);

        Assert.False(viewModel.TransformService.IsVisible);

        viewModel.ModeService.AnnotationMode.CurrentTool = "Move";

        Assert.True(viewModel.TransformService.IsVisible);
    }

    [Fact]
    public void ModeChange_RaisesSelectionResetRequest()
    {
        var viewModel = new MainWindowViewModel();
        int resetRequests = 0;
        viewModel.SelectionResetRequested += () => resetRequests++;

        viewModel.ModeService.SetMode("Annotation");

        Assert.Equal(1, resetRequests);
    }
}
