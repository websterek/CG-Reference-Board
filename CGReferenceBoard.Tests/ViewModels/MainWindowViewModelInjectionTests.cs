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
        IHistoryService historySvc = new HistoryService();
        ILocalizationService localizationSvc = new LocalizationService();
        INotificationService notificationSvc = new NotificationService();
        var modeService = new ModeService();
        var selectionService = new SelectionService();
        var transformService = new TransformService();

        var vm = new MainWindowViewModel(
            isViewMode: false,
            modeService: modeService,
            selectionService: selectionService,
            transformService: transformService,
            boardService: boardSvc,
            historyService: historySvc,
            localizationService: localizationSvc,
            notificationService: notificationSvc);

        Assert.NotNull(vm);
    }
}
