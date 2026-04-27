using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

public class HistoryService : HistoryManager, IHistoryService
{
    public HistoryService() : base(100, () => { })
    {
    }

    public new void Execute(IUndoCommand command)
    {
        Commit(command);
    }
}