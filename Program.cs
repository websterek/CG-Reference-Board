using Avalonia;
using Avalonia.Skia;
using System;
using System.Diagnostics;

namespace CGReferenceBoard;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Route Trace output (used by Avalonia's LogToTrace) to the console so logs are visible
        Trace.Listeners.Add(new ConsoleTraceListener());

        // Optional: make sure console uses UTF-8 output encoding where supported
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // If setting encoding fails (platform-specific), continue without throwing.
        }

        // Test message to verify console is attached when running via `dotnet run -p:OutputType=Exe`
        Console.WriteLine("Starting CGReferenceBoard (console output enabled).");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 });
}
