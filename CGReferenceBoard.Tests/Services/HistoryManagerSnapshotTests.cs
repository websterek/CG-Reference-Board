using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services;

public class HistoryManagerSnapshotTests
{
    public class GroupResizeCommandTests
    {
        [Fact]
        public void Execute_AppliesNewColSpanAndRowSpanToAllCells()
        {
            var cell1 = new CellViewModel { ColSpan = 1, RowSpan = 1 };
            var cell2 = new CellViewModel { ColSpan = 2, RowSpan = 2 };
            var resizes = new List<(CellViewModel, int, int, int, int)>
            {
                (cell1, 1, 1, 3, 4),
                (cell2, 2, 2, 5, 6)
            };
            var command = new GroupResizeCommand(resizes);

            command.Execute();

            Assert.Equal(3, cell1.ColSpan);
            Assert.Equal(4, cell1.RowSpan);
            Assert.Equal(5, cell2.ColSpan);
            Assert.Equal(6, cell2.RowSpan);
        }

        [Fact]
        public void Execute_DoesNothingWhenNoResizes()
        {
            var resizes = new List<(CellViewModel, int, int, int, int)>();
            var command = new GroupResizeCommand(resizes);

            command.Execute();

            Assert.True(true);
        }

        [Fact]
        public void Undo_RestoresOldColSpanAndRowSpanToAllCells()
        {
            var cell1 = new CellViewModel { ColSpan = 3, RowSpan = 4 };
            var cell2 = new CellViewModel { ColSpan = 5, RowSpan = 6 };
            var resizes = new List<(CellViewModel, int, int, int, int)>
            {
                (cell1, 1, 1, 3, 4),
                (cell2, 2, 2, 5, 6)
            };
            var command = new GroupResizeCommand(resizes);

            command.Undo();

            Assert.Equal(1, cell1.ColSpan);
            Assert.Equal(1, cell1.RowSpan);
            Assert.Equal(2, cell2.ColSpan);
            Assert.Equal(2, cell2.RowSpan);
        }
    }

    public class GroupDragCommandTests
    {
        [Fact]
        public void Execute_MovesAllCellsAndAnnotationsToNewPosition()
        {
            var cell = new CellViewModel { CanvasX = 1, CanvasY = 2 };
            var ann = new AnnotationViewModel { CanvasX = 3, CanvasY = 4 };
            var cellMoves = new List<(CellViewModel, double, double, double, double)>
            {
                (cell, 1, 2, 100, 200)
            };
            var annotationMoves = new List<(AnnotationViewModel, double, double, double, double)>
            {
                (ann, 3, 4, 300, 400)
            };
            var command = new GroupDragCommand(
                new ObservableCollection<CellViewModel>(),
                new ObservableCollection<AnnotationViewModel>(),
                cellMoves,
                annotationMoves);

            command.Execute();

            Assert.Equal(100, cell.CanvasX);
            Assert.Equal(200, cell.CanvasY);
            Assert.Equal(300, ann.CanvasX);
            Assert.Equal(400, ann.CanvasY);
        }

        [Fact]
        public void Undo_RestoresAllCellsAndAnnotationsToOldPosition()
        {
            var cell = new CellViewModel { CanvasX = 100, CanvasY = 200 };
            var ann = new AnnotationViewModel { CanvasX = 300, CanvasY = 400 };
            var cellMoves = new List<(CellViewModel, double, double, double, double)>
            {
                (cell, 1, 2, 100, 200)
            };
            var annotationMoves = new List<(AnnotationViewModel, double, double, double, double)>
            {
                (ann, 3, 4, 300, 400)
            };
            var command = new GroupDragCommand(
                new ObservableCollection<CellViewModel>(),
                new ObservableCollection<AnnotationViewModel>(),
                cellMoves,
                annotationMoves);

            command.Undo();

            Assert.Equal(1, cell.CanvasX);
            Assert.Equal(2, cell.CanvasY);
            Assert.Equal(3, ann.CanvasX);
            Assert.Equal(4, ann.CanvasY);
        }
    }

    public class CompositeCommandTests
    {
        [Fact]
        public void Execute_RunsAllCommandsInOrder()
        {
            var cell = new CellViewModel();
            var cmd1 = new MoveCellCommand(cell, 0, 0, 10, 10);
            var cmd2 = new MoveCellCommand(cell, 10, 10, 20, 20);
            var composite = new CompositeCommand(new List<IUndoCommand> { cmd1, cmd2 }, "Test");

            composite.Execute();

            Assert.Equal(20, cell.CanvasX);
            Assert.Equal(20, cell.CanvasY);
        }

        [Fact]
        public void Undo_ReverseAllCommandsInReverseOrder()
        {
            var cell = new CellViewModel { CanvasX = 20, CanvasY = 20 };
            var cmd1 = new MoveCellCommand(cell, 0, 0, 10, 10);
            var cmd2 = new MoveCellCommand(cell, 10, 10, 20, 20);
            var composite = new CompositeCommand(new List<IUndoCommand> { cmd1, cmd2 }, "Test");

            composite.Undo();

            Assert.Equal(0, cell.CanvasX);
            Assert.Equal(0, cell.CanvasY);
        }

        [Fact]
        public void Description_ReturnsProvidedDescription()
        {
            var cmd1 = new MoveCellCommand(new CellViewModel(), 0, 0, 1, 1);
            var composite = new CompositeCommand(new List<IUndoCommand> { cmd1 }, "My Test");

            Assert.Equal("My Test", composite.Description);
        }
    }

    public class HistoryManagerIntegrationTests
    {
        [Fact]
        public void HistoryManager_CommitsAndExecutesCommand()
        {
            var manager = new HistoryManager(10, () => { });
            var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
            var cmd = new MoveCellCommand(cell, 0, 0, 100, 200);

            manager.Commit(cmd);

            Assert.Equal(100, cell.CanvasX);
            Assert.Equal(200, cell.CanvasY);
            Assert.True(manager.CanUndo);
            Assert.False(manager.CanRedo);
        }

        [Fact]
        public void HistoryManager_UndoRestoresPreviousState()
        {
            var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
            var manager = new HistoryManager(10, () => { });
            manager.Commit(new MoveCellCommand(cell, 0, 0, 100, 200));

            manager.Undo();

            Assert.Equal(0, cell.CanvasX);
            Assert.Equal(0, cell.CanvasY);
            Assert.False(manager.CanUndo);
            Assert.True(manager.CanRedo);
        }

        [Fact]
        public void HistoryManager_RedoReAppliesState()
        {
            var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
            var manager = new HistoryManager(10, () => { });
            manager.Commit(new MoveCellCommand(cell, 0, 0, 100, 200));
            manager.Undo();

            manager.Redo();

            Assert.Equal(100, cell.CanvasX);
            Assert.Equal(200, cell.CanvasY);
            Assert.True(manager.CanUndo);
            Assert.False(manager.CanRedo);
        }

        [Fact]
        public void HistoryManager_ClearRemovesAllHistory()
        {
            var cell = new CellViewModel { CanvasX = 0, CanvasY = 0 };
            var manager = new HistoryManager(10, () => { });
            manager.Commit(new MoveCellCommand(cell, 0, 0, 100, 200));

            manager.Clear();

            Assert.False(manager.CanUndo);
            Assert.False(manager.CanRedo);
        }

        [Fact]
        public void HistoryManager_DoesNotExceedMaxDepth()
        {
            var cell = new CellViewModel();
            var manager = new HistoryManager(3, () => { });

            for (int i = 0; i < 5; i++)
            {
                manager.Commit(new MoveCellCommand(cell, i, i, i + 1, i + 1));
            }

            Assert.Equal(3, manager.UndoCount);
        }
    }
}