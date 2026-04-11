using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace CGReferenceBoard.Controls;

/// <summary>
/// A <see cref="NativeControlHost"/> subclass that exposes the created platform handle
/// via a callback, for use with LibVLC native rendering.
/// Reserved for future built-in video player feature.
/// </summary>
public class CustomVlcHost : NativeControlHost
{
    /// <summary>
    /// Callback invoked when the native platform handle is created.
    /// </summary>
    public Action<IPlatformHandle>? OnHandleCreated;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        OnHandleCreated?.Invoke(handle);
        return handle;
    }
}
