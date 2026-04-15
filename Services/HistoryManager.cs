using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

public class HistoryManager
{
    private readonly Stack<IUndoCommand> _undoStack = new();
    private readonly Stack<IUndoCommand> _redoStack = new();
    private readonly int _maxDepth;
    private readonly Action _onStateChanged;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public HistoryManager(int maxDepth, Action onStateChanged)
    {
        _maxDepth = maxDepth;
        _onStateChanged = onStateChanged;
    }

    public void Commit(IUndoCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        while (_undoStack.Count > _maxDepth)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < _maxDepth; i++)
                _undoStack.Push(items[i]);
        }

        _onStateChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
            return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        _onStateChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
            return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        _onStateChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _onStateChanged?.Invoke();
    }
}

public class GroupDragCommand : IUndoCommand
{
    private readonly List<(CellViewModel Cell, double OldX, double OldY, double NewX, double NewY)> _cellMoves;
    private readonly List<(AnnotationViewModel Ann, double OldX, double OldY, double NewX, double NewY)> _annotationMoves;
    private readonly ObservableCollection<CellViewModel> _gridCells;
    private readonly ObservableCollection<AnnotationViewModel> _annotations;

    public string Description => "Move";

    public GroupDragCommand(
        ObservableCollection<CellViewModel> gridCells,
        ObservableCollection<AnnotationViewModel> annotations,
        List<(CellViewModel Cell, double OldX, double OldY, double NewX, double NewY)> cellMoves,
        List<(AnnotationViewModel Ann, double OldX, double OldY, double NewX, double NewY)> annotationMoves)
    {
        _gridCells = gridCells;
        _annotations = annotations;
        _cellMoves = cellMoves;
        _annotationMoves = annotationMoves;
    }

    public void Execute()
    {
        foreach (var (cell, _, _, newX, newY) in _cellMoves)
        {
            cell.CanvasX = newX;
            cell.CanvasY = newY;
        }
        foreach (var (ann, _, _, newX, newY) in _annotationMoves)
        {
            ann.CanvasX = newX;
            ann.CanvasY = newY;
        }
    }

    public void Undo()
    {
        foreach (var (cell, oldX, oldY, _, _) in _cellMoves)
        {
            cell.CanvasX = oldX;
            cell.CanvasY = oldY;
        }
        foreach (var (ann, oldX, oldY, _, _) in _annotationMoves)
        {
            ann.CanvasX = oldX;
            ann.CanvasY = oldY;
        }
    }
}

public class GroupResizeCommand : IUndoCommand
{
    private readonly List<(CellViewModel Cell, int OldCols, int OldRows, int NewCols, int NewRows)> _resizes;

    public string Description => "Resize";

    public GroupResizeCommand(List<(CellViewModel Cell, int OldCols, int OldRows, int NewCols, int NewRows)> resizes)
    {
        _resizes = resizes;
    }

    public void Execute()
    {
        foreach (var (_, _, _, newCols, newRows) in _resizes)
        {
            // Need to find the cell by reference
        }
    }

    public void Undo()
    {
        foreach (var (cell, oldCols, oldRows, _, _) in _resizes)
        {
            cell.ColSpan = oldCols;
            cell.RowSpan = oldRows;
        }
    }
}

public class CompositeCommand : IUndoCommand
{
    private readonly List<IUndoCommand> _commands;
    private readonly string _description;

    public string Description => _description;

    public CompositeCommand(List<IUndoCommand> commands, string description)
    {
        _commands = commands;
        _description = description;
    }

    public void Execute()
    {
        foreach (var cmd in _commands)
            cmd.Execute();
    }

    public void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
            _commands[i].Undo();
    }
}