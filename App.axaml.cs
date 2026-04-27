using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CGReferenceBoard.Modes;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
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

            var boardService = new BoardService();
            var modeService = new ModeService();
            var selectionService = new SelectionService();
            var transformService = new TransformService();
            var vm = new MainWindowViewModel(isViewMode, modeService, selectionService, transformService, boardService);

            var window = new MainWindow(vm);
            if (!string.IsNullOrEmpty(startFile))
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.LoadBoardFromFile(startFile));
            desktop.MainWindow = window;
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Debug.WriteLine($"Application exiting with code: {e.ApplicationExitCode}");
    }
}
