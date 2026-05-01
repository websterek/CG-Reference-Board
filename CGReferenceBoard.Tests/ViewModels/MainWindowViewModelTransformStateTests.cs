using Avalonia;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.ViewModels;

public sealed class MainWindowViewModelTransformStateTests
{
    static MainWindowViewModelTransformStateTests()
    {
        // Sweep stale /tmp/{Guid:N}.cgrb files left behind by aborted earlier runs
        // of this test class (parameterless-ctor tests use temp paths; a crash
        // between WriteAllText and File.Delete would orphan the file).
        try
        {
            var rx = new Regex("^[0-9a-fA-F]{32}\\.cgrb$", RegexOptions.Compiled);
            foreach (var f in Directory.EnumerateFiles(Path.GetTempPath(), "*.cgrb"))
            {
                if (!rx.IsMatch(Path.GetFileName(f))) continue;
                try { File.Delete(f); } catch { /* best effort */ }
            }
        }
        catch { /* best effort */ }
    }
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
    public void ModeAndAnnotationToolChanges_RaiseTransformContextChanging()
    {
        var viewModel = new MainWindowViewModel();
        int changeCount = 0;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.TransformContextVersion))
                changeCount++;
        };

        viewModel.ModeService.SetMode("Annotation");
        viewModel.ModeService.AnnotationMode.CurrentTool = "Move";

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void ModeChange_ClearsSelection()
    {
        var viewModel = new MainWindowViewModel();
        // Pre-select something so we can verify it was cleared.
        var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
        viewModel.GridCells.Add(cell);
        viewModel.SelectionService.SelectCell(cell);
        Assert.True(viewModel.SelectionService.HasSelection);

        viewModel.ModeService.SetMode("Annotation");

        Assert.False(viewModel.SelectionService.HasSelection);
    }

    [Fact]
    public void RestoreBoardState_ClearsSelection()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.SetBoardFilePath(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cgrb"));
        viewModel.RestoreBoardState("{\"Cells\":[],\"Annotations\":[]}");

        Assert.False(viewModel.SelectionService.HasSelection);
    }

    [Fact]
    public async Task LoadBoardFromFile_ClearsSelection()
    {
        var viewModel = new MainWindowViewModel();
        var boardPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.cgrb");
        await File.WriteAllTextAsync(boardPath, "{\"Cells\":[],\"Annotations\":[]}");

        try
        {
            await viewModel.LoadBoardFromFileAsync(boardPath);
            Assert.False(viewModel.SelectionService.HasSelection);
        }
        finally
        {
            File.Delete(boardPath);
        }
    }

    [Fact]
    public void ResetInteractionState_ClearsSelectionAndTransformState()
    {
        var viewModel = new MainWindowViewModel();
        var annotation = new AnnotationViewModel { CanvasX = 20, CanvasY = 30, Type = "Brush" };
        annotation.Points.Add(new Point(0, 0));
        annotation.Points.Add(new Point(30, 30));
        annotation.UpdateBoundsCache();

        int resetRequests = 0;
        viewModel.SelectionService.SelectionChanged += (_, _) =>
        {
            if (!viewModel.SelectionService.HasSelection) resetRequests++;
        };

        viewModel.ModeService.SetMode("Grid");
        viewModel.Annotations.Add(annotation);
        viewModel.SelectionService.SelectAnnotation(annotation);
        viewModel.TransformService.BeginMove(
            new Point(20, 30),
            viewModel.SelectionService,
            new[] { TransformItemSnapshot.FromAnnotation(annotation, new Rect(annotation.AbsBoundsLeft, annotation.AbsBoundsTop, annotation.AbsBoundsRight - annotation.AbsBoundsLeft, annotation.AbsBoundsBottom - annotation.AbsBoundsTop)) });

        viewModel.ResetInteractionState();

        Assert.True(resetRequests >= 1);
        Assert.Empty(viewModel.SelectionService.SelectedCells);
        Assert.Empty(viewModel.SelectionService.SelectedAnnotations);
        Assert.False(viewModel.SelectionService.HasSelection);
        Assert.False(viewModel.TransformService.HasActiveOperation);
        Assert.False(viewModel.TransformService.IsVisible);
        Assert.Empty(viewModel.TransformService.ActiveSnapshots);
        Assert.Equal(TransformCapabilities.None, viewModel.TransformService.Capabilities);
        Assert.Equal(TransformOperation.None, viewModel.TransformService.Operation);
    }
}
