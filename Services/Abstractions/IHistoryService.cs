using CGReferenceBoard.Services;

namespace CGReferenceBoard.Services.Abstractions;

public interface IHistoryService
{
    int UndoCount { get; }
    int RedoCount { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Execute(IUndoCommand command);
    void Undo();
    void Redo();
    void Clear();
}