using Avalonia;
using Avalonia.Skia;
using System;

namespace CGReferenceBoard;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 });
}