using Avalonia;
using Avalonia.Skia;
using CGReferenceBoard;
using System;

namespace CGReferenceBoard.Tests.TestInfrastructure;

internal static class AvaloniaTestApp
{
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .With(new SkiaOptions { MaxGpuResourceSizeBytes = 256 * 1024 * 1024 })
                .SetupWithoutStarting();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
        {
            // Another test already initialized Avalonia in this process.
        }

        _initialized = true;
    }
}
