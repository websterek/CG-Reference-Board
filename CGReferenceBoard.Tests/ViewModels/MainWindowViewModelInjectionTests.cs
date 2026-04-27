// CGReferenceBoard.Tests/ViewModels/MainWindowViewModelInjectionTests.cs
using CGReferenceBoard.Modes;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.ViewModels;

public class MainWindowViewModelInjectionTests
{
    [Fact]
    public void InjectedCtor_AcceptsBoardService()
    {
        IBoardService boardSvc = new BoardService();
        var modeService = new ModeService();
        var selectionService = new SelectionService();
        var transformService = new TransformService();

        var vm = new MainWindowViewModel(
            isViewMode: false,
            modeService: modeService,
            selectionService: selectionService,
            transformService: transformService,
            boardService: boardSvc);

        Assert.NotNull(vm);
    }
}
