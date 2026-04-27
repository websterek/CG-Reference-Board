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
    public void Constructor_SetsDataContextToSelf()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var dialog = new TextInputDialog("Test Title", "Initial text");
            Assert.Same(dialog, dialog.DataContext);
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public void Constructor_SetsDialogTitle()
    {
        TextInputDialog? dialog = null;
        Dispatcher.UIThread.InvokeAsync(() => dialog = new TextInputDialog("My Title", ""))
            .GetAwaiter().GetResult();
        Assert.Equal("My Title", dialog!.DialogTitle);
    }

    [Fact]
    public void Constructor_SetsInputText()
    {
        TextInputDialog? dialog = null;
        Dispatcher.UIThread.InvokeAsync(() => dialog = new TextInputDialog("T", "Hello"))
            .GetAwaiter().GetResult();
        Assert.Equal("Hello", dialog!.InputText);
    }
}
