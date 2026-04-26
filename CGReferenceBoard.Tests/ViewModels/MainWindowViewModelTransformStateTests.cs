using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
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

    [Fact]
    public void RestoreBoardState_RaisesSelectionResetRequest()
    {
        var viewModel = new MainWindowViewModel();
        int resetRequests = 0;
        viewModel.SelectionResetRequested += () => resetRequests++;

        viewModel.SetBoardFilePath(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cgrb"));
        viewModel.RestoreBoardState("{\"Cells\":[],\"Annotations\":[]}");

        Assert.Equal(1, resetRequests);
    }

    [Fact]
    public async Task LoadBoardFromFile_RaisesSelectionResetRequest()
    {
        var viewModel = new MainWindowViewModel();
        var boardPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cgrb");
        await File.WriteAllTextAsync(boardPath, "{\"Cells\":[],\"Annotations\":[]}");

        try
        {
            var resetRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            viewModel.SelectionResetRequested += () => resetRaised.TrySetResult();

            viewModel.LoadBoardFromFile(boardPath);

            var completed = await Task.WhenAny(resetRaised.Task, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(resetRaised.Task, completed);
        }
        finally
        {
            File.Delete(boardPath);
        }
    }
}
