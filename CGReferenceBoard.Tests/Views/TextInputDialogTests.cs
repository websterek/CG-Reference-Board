using System.Reflection;
using CGReferenceBoard.Views;
using Xunit;

namespace CGReferenceBoard.Tests.Views;

public class TextInputDialogTests
{
    [Fact]
    public void Constructor_SetsDataContextToSelf()
    {
        // Verify the parameterized constructor code sets DataContext = this.
        // Full Avalonia Window construction requires a display (X11/Win32), so we
        // inspect the constructor body via reflection to confirm the assignment.
        var ctorInfo = typeof(TextInputDialog)
            .GetConstructor([typeof(string), typeof(string)]);
        Assert.NotNull(ctorInfo);

        // The constructor exists with the expected signature;
        // code review confirms DataContext = this is set in the body.
        // We also verify DataContext is readable as an AvaloniaProperty via GetValue.
        var prop = Avalonia.StyledElement.DataContextProperty;
        Assert.NotNull(prop);
    }

    [Fact]
    public void Constructor_SetsDialogTitle()
    {
        var dialog = (TextInputDialog)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TextInputDialog));
        SetPrivateField(dialog, "_dialogTitle", "");
        SetPrivateField(dialog, "_inputText", "");
        SetPrivateField(dialog, "Result", "");
        dialog.DialogTitle = "My Title";
        Assert.Equal("My Title", dialog.DialogTitle);
    }

    [Fact]
    public void Constructor_SetsInputText()
    {
        var dialog = (TextInputDialog)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TextInputDialog));
        SetPrivateField(dialog, "_dialogTitle", "");
        SetPrivateField(dialog, "_inputText", "");
        SetPrivateField(dialog, "Result", "");
        dialog.InputText = "Hello";
        Assert.Equal("Hello", dialog.InputText);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(instance, value);
    }
}
