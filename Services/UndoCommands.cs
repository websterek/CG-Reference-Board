using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

public interface IUndoCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

public abstract class UndoCommandBase : IUndoCommand
{
    public abstract string Description { get; }
    public abstract void Execute();
    public abstract void Undo();
}

public class MoveCellCommand : UndoCommandBase
{
    private readonly CellViewModel _cell;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public override string Description => "Move cell";

    public MoveCellCommand(CellViewModel cell, double oldX, double oldY, double newX, double newY)
    {
        _cell = cell;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public override void Execute()
    {
        _cell.CanvasX = _newX;
        _cell.CanvasY = _newY;
    }

    public override void Undo()
    {
        _cell.CanvasX = _oldX;
        _cell.CanvasY = _oldY;
    }
}

public class ResizeCellCommand : UndoCommandBase
{
    private readonly CellViewModel _cell;
    private readonly int _oldCols, _oldRows;
    private readonly int _newCols, _newRows;

    public override string Description => "Resize cell";

    public ResizeCellCommand(CellViewModel cell, int oldCols, int oldRows, int newCols, int newRows)
    {
        _cell = cell;
        _oldCols = oldCols;
        _oldRows = oldRows;
        _newCols = newCols;
        _newRows = newRows;
    }

    public override void Execute()
    {
        _cell.ColSpan = _newCols;
        _cell.RowSpan = _newRows;
    }

    public override void Undo()
    {
        _cell.ColSpan = _oldCols;
        _cell.RowSpan = _oldRows;
    }
}

public class AddCellCommand : UndoCommandBase
{
    private readonly ObservableCollection<CellViewModel> _cells;
    private readonly CellViewModel _cell;

    public override string Description => "Add cell";

    public AddCellCommand(ObservableCollection<CellViewModel> cells, CellViewModel cell)
    {
        _cells = cells;
        _cell = cell;
    }

    public override void Execute()
    {
        _cells.Add(_cell);
    }

    public override void Undo()
    {
        _cells.Remove(_cell);
    }
}

public class DeleteCellCommand : UndoCommandBase
{
    private readonly ObservableCollection<CellViewModel> _cells;
    private readonly CellViewModel _cell;
    private readonly int _index;

    public override string Description => "Delete cell";

    public DeleteCellCommand(ObservableCollection<CellViewModel> cells, CellViewModel cell)
    {
        _cells = cells;
        _cell = cell;
        _index = cells.IndexOf(cell);
    }

    public override void Execute()
    {
        _cells.Remove(_cell);
    }

    public override void Undo()
    {
        if (_index >= 0 && _index <= _cells.Count)
            _cells.Insert(_index, _cell);
        else
            _cells.Add(_cell);
    }
}

public class MoveAnnotationCommand : UndoCommandBase
{
    private readonly AnnotationViewModel _annotation;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public override string Description => "Move annotation";

    public MoveAnnotationCommand(AnnotationViewModel annotation, double oldX, double oldY, double newX, double newY)
    {
        _annotation = annotation;
        _oldX = oldX;
        _oldY = oldY;
        _newX = newX;
        _newY = newY;
    }

    public override void Execute()
    {
        _annotation.CanvasX = _newX;
        _annotation.CanvasY = _newY;
    }

    public override void Undo()
    {
        _annotation.CanvasX = _oldX;
        _annotation.CanvasY = _oldY;
    }
}

public class AddAnnotationCommand : UndoCommandBase
{
    private readonly ObservableCollection<AnnotationViewModel> _annotations;
    private readonly AnnotationViewModel _annotation;

    public override string Description => "Add annotation";

    public AddAnnotationCommand(ObservableCollection<AnnotationViewModel> annotations, AnnotationViewModel annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
    }

    public override void Execute()
    {
        _annotations.Add(_annotation);
    }

    public override void Undo()
    {
        _annotations.Remove(_annotation);
    }
}

public class DeleteAnnotationCommand : UndoCommandBase
{
    private readonly ObservableCollection<AnnotationViewModel> _annotations;
    private readonly AnnotationViewModel _annotation;
    private readonly int _index;

    public override string Description => "Delete annotation";

    public DeleteAnnotationCommand(ObservableCollection<AnnotationViewModel> annotations, AnnotationViewModel annotation)
    {
        _annotations = annotations;
        _annotation = annotation;
        _index = annotations.IndexOf(annotation);
    }

    public override void Execute()
    {
        _annotations.Remove(_annotation);
    }

    public override void Undo()
    {
        if (_index >= 0 && _index <= _annotations.Count)
            _annotations.Insert(_index, _annotation);
        else
            _annotations.Add(_annotation);
    }
}

public class ModifyTextContentCommand : UndoCommandBase
{
    private readonly CellViewModel _cell;
    private readonly string _oldText;
    private readonly string _newText;

    public override string Description => "Edit text";

    public ModifyTextContentCommand(CellViewModel cell, string oldText, string newText)
    {
        _cell = cell;
        _oldText = oldText;
        _newText = newText;
    }

    public override void Execute()
    {
        _cell.TextContent = _newText;
    }

    public override void Undo()
    {
        _cell.TextContent = _oldText;
    }
}

public class ModifyAnnotationTextCommand : UndoCommandBase
{
    private readonly AnnotationViewModel _annotation;
    private readonly string _oldText;
    private readonly string _newText;

    public override string Description => "Edit annotation text";

    public ModifyAnnotationTextCommand(AnnotationViewModel annotation, string oldText, string newText)
    {
        _annotation = annotation;
        _oldText = oldText;
        _newText = newText;
    }

    public override void Execute()
    {
        _annotation.Text = _newText;
    }

    public override void Undo()
    {
        _annotation.Text = _oldText;
    }
}

public class ModifyCellColorCommand : UndoCommandBase
{
    private readonly CellViewModel _cell;
    private readonly string _oldBackgroundColor;
    private readonly string _newBackgroundColor;
    private readonly string _oldForegroundColor;
    private readonly string _newForegroundColor;

    public override string Description => "Change cell color";

    public ModifyCellColorCommand(CellViewModel cell, string oldBg, string newBg, string oldFg, string newFg)
    {
        _cell = cell;
        _oldBackgroundColor = oldBg;
        _newBackgroundColor = newBg;
        _oldForegroundColor = oldFg;
        _newForegroundColor = newFg;
    }

    public override void Execute()
    {
        _cell.BackgroundColor = _newBackgroundColor;
        _cell.ForegroundColor = _newForegroundColor;
    }

    public override void Undo()
    {
        _cell.BackgroundColor = _oldBackgroundColor;
        _cell.ForegroundColor = _oldForegroundColor;
    }
}

public class BatchCommand : UndoCommandBase
{
    private readonly List<IUndoCommand> _commands;

    public override string Description => "Multiple changes";

    public BatchCommand(List<IUndoCommand> commands)
    {
        _commands = commands;
    }

    public override void Execute()
    {
        foreach (var cmd in _commands)
            cmd.Execute();
    }

    public override void Undo()
    {
        for (int i = _commands.Count - 1; i >= 0; i--)
            _commands[i].Undo();
    }
}