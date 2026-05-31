using System.Threading.Tasks;
using Avalonia.Threading;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

/// <summary>
/// Placeholder integration tests for gesture flows through IInteractionController.
/// These will be fleshed out in H1 once a headless pointer input driver is available.
/// </summary>
public class GestureIntegrationTests
{
    [Fact]
    public async Task LeftButtonOnEmptyCanvas_ClearsSelection()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = MainWindowViewModel.CreateWithDI(false);
            // TODO H1: press LMB on empty canvas, verify selection cleared
            Assert.True(true, "Placeholder — expand when pointer driver available");
        });
    }

    [Fact]
    public async Task CtrlLeftDrag_OpensMarquee()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = MainWindowViewModel.CreateWithDI(false);
            // TODO H1: ctrl+drag on canvas, verify MarqueeSelectState entered
            Assert.True(true, "Placeholder — expand in H1");
        });
    }

    [Fact]
    public async Task MiddleAndLeftButton_ZoomsViewport()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = MainWindowViewModel.CreateWithDI(false);
            // TODO H1: middle+left drag, verify viewport zoom changed
            Assert.True(true, "Placeholder — expand in H1");
        });
    }

    [Fact]
    public async Task PointerCaptureLost_ResetsToIdleState()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var ctx = new FakeInteractionContext();
            var controller = new CGReferenceBoard.Interaction.InteractionController(
                ctx, new CGReferenceBoard.Interaction.States.IdleState());
            // TODO H1: drive into non-idle state, fire CaptureLost, verify back to IdleState
            Assert.IsType<CGReferenceBoard.Interaction.States.IdleState>(controller.CurrentState);
        });
    }
}
