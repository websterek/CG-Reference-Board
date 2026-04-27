using Avalonia.Threading;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.Views;
using Xunit;

namespace CGReferenceBoard.Tests.Views;

public class TextInputDialogTests
{
    public TextInputDialogTests()
    {
        AvaloniaTestApp.EnsureInitialized();
    }

    [Fact]
    public async Task Constructor_SetsDataContextToSelf()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var dialog = new TextInputDialog("Test Title", "Initial text");
            Assert.Same(dialog, dialog.DataContext);
        });
    }

    [Fact]
    public async Task Constructor_SetsDialogTitle()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var dialog = new TextInputDialog("My Title", "");
            Assert.Equal("My Title", dialog.DialogTitle);
        });
    }

    [Fact]
    public async Task Constructor_SetsInputText()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var dialog = new TextInputDialog("T", "Hello");
            Assert.Equal("Hello", dialog.InputText);
        });
    }
}
