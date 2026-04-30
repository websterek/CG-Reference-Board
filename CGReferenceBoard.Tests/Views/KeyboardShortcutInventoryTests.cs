using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Views;

/// <summary>
/// Characterization tests that document every keyboard shortcut in Window_KeyDown
/// which delegates to an IRelayCommand on MainWindowViewModel.
/// </summary>
public class KeyboardShortcutInventoryTests
{
    public static IEnumerable<object[]> ShortcutInventory()
    {
        // ── Undo / Redo ──────────────────────────────────────────────────────
        yield return new object[] { "Ctrl+Z",              nameof(MainWindowViewModel.UndoCommand) };
        yield return new object[] { "Ctrl+Shift+Z",        nameof(MainWindowViewModel.RedoCommand) };
        yield return new object[] { "Ctrl+Y",              nameof(MainWindowViewModel.RedoCommand) };

        // ── Mode switching ───────────────────────────────────────────────────
        yield return new object[] { "Ctrl+1",              nameof(MainWindowViewModel.SwitchToGridModeCommand) };
        yield return new object[] { "Ctrl+2",              nameof(MainWindowViewModel.SwitchToAnnotationModeCommand) };

        // ── Annotation visibility ────────────────────────────────────────────
        yield return new object[] { "Shift+A",             nameof(MainWindowViewModel.ToggleAnnotationsVisibleCommand) };

        // ── Annotation tools (draw-mode only) ───────────────────────────────
        yield return new object[] { "B (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "E (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "T (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "L (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "U (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "O (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };
        yield return new object[] { "V (draw mode)",       nameof(MainWindowViewModel.SetAnnotationToolCommand) };

        // ── Window always-on-top ─────────────────────────────────────────────
        yield return new object[] { "Ctrl+Shift+T",        nameof(MainWindowViewModel.ToggleAlwaysOnTopCommand) };
    }

    [Theory]
    [MemberData(nameof(ShortcutInventory))]
    public void CommandExistsOnViewModel(string gesture, string commandPropertyName)
    {
        var prop = typeof(MainWindowViewModel).GetProperty(commandPropertyName);
        Assert.True(prop is not null,
            $"Shortcut '{gesture}' expects '{commandPropertyName}' on MainWindowViewModel but property not found.");
        Assert.True(typeof(IRelayCommand).IsAssignableFrom(prop!.PropertyType),
            $"'{commandPropertyName}' is not an IRelayCommand.");
    }
}
