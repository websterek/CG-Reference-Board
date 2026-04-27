using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using CGReferenceBoard;
using System;
using System.Threading;

namespace CGReferenceBoard.Tests.TestInfrastructure;

internal static class AvaloniaTestApp
{
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            // Initialize Avalonia on a dedicated long-lived thread so that
            // Dispatcher.UIThread is consistently bound to one thread across
            // all tests in the process, regardless of which xUnit worker
            // thread calls EnsureInitialized().
            var ready = new ManualResetEventSlim(false);
            Exception? initException = null;

            var uiThread = new Thread(() =>
            {
                try
                {
                    AppBuilder.Configure<App>()
                        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
                        .WithInterFont()
                        .SetupWithoutStarting();
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
                {
                    // Another test already initialized Avalonia in this process.
                }
                catch (Exception ex)
                {
                    initException = ex;
                }

                ready.Set();

                // Keep the thread alive and pumping the dispatcher so that
                // Dispatcher.UIThread.Post(…) / InvokeAsync(…) can be serviced.
                Dispatcher.UIThread.MainLoop(CancellationToken.None);
            })
            {
                IsBackground = true,
                Name = "Avalonia UIThread (test)"
            };

            uiThread.Start();
            ready.Wait();

            if (initException is not null)
            {
                throw new InvalidOperationException("Avalonia test initialization failed.", initException);
            }

            _initialized = true;
        }
    }
}
