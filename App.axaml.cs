using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CGReferenceBoard.Views;
using System;
using System.Diagnostics;
using System.Linq;

namespace CGReferenceBoard;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            bool isViewMode = desktop.Args?.Contains("--view") == true;
            string? startFile = desktop.Args?.FirstOrDefault(arg => !arg.StartsWith("-"));

            desktop.MainWindow = new MainWindow(isViewMode, startFile);
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Debug.WriteLine($"Application exiting with code: {e.ApplicationExitCode}");
    }
}
