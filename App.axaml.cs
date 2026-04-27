using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CGReferenceBoard.Composition;
using CGReferenceBoard.Controls;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;
using CGReferenceBoard.Views;
using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace CGReferenceBoard;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddCgReferenceBoard();
            Services = services.BuildServiceProvider();

            var effectService = Services.GetRequiredService<IAnnotationEffectService>();
            AnnotationShape.SetEffectService(effectService);

            bool isViewMode = desktop.Args?.Contains("--view") == true;
            string? startFile = desktop.Args?.FirstOrDefault(arg => !arg.StartsWith("-"));

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var window = Services.GetRequiredService<MainWindow>();
            
            if (!string.IsNullOrEmpty(startFile))
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = vm.LoadBoardFromFileAsync(startFile));
            
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