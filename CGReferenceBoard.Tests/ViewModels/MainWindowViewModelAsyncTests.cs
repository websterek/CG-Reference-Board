using System.Threading.Tasks;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.ViewModels;

public class MainWindowViewModelAsyncTests
{
    private MainWindowViewModel CreateVm() => new MainWindowViewModel(isViewMode: false);

    [Fact]
    public async Task LoadRecentBoardsAsync_DoesNotThrow()
    {
        var vm = CreateVm();
        await vm.LoadRecentBoardsAsync();
    }

    [Fact]
    public async Task LoadUserSettingsAsync_DoesNotThrow()
    {
        var vm = CreateVm();
        await vm.LoadUserSettingsAsync();
    }

    [Fact]
    public async Task SaveUserSettingsAsync_DoesNotThrow()
    {
        var vm = CreateVm();
        await vm.SaveUserSettingsAsync();
    }

    [Fact]
    public async Task UpdateBoardDirectoryListAsync_DoesNotThrow()
    {
        var vm = CreateVm();
        await vm.UpdateBoardDirectoryListAsync();
    }
}
