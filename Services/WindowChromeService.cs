using System.ComponentModel;
using Avalonia.Controls;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

/// <summary>
/// Live implementation backed by an Avalonia Window.
/// Must be constructed on the UI thread and after the window exists.
/// </summary>
public sealed class WindowChromeService : IWindowChromeService
{
    private readonly Window _window;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public WindowChromeService(Window window)
    {
        _window = window;
    }

    public bool IsAlwaysOnTop
    {
        get => _window.Topmost;
        set
        {
            _window.Topmost = value;
            Notify(nameof(IsAlwaysOnTop));
        }
    }

    public double Opacity
    {
        get => _window.Opacity;
        set
        {
            _window.Opacity = value;
            Notify(nameof(Opacity));
        }
    }

    public bool ShowDecorations
    {
        get => _window.WindowDecorations != Avalonia.Controls.WindowDecorations.None;
        set
        {
            _window.WindowDecorations = value
                ? Avalonia.Controls.WindowDecorations.Full
                : Avalonia.Controls.WindowDecorations.None;
            Notify(nameof(ShowDecorations));
        }
    }
}
