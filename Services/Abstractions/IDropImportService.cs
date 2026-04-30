using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Handles drop-import from the OS (files, URLs, HTML snippets).
/// </summary>
public interface IDropImportService
{
    /// <summary>
    /// Process an OS drop event. The <paramref name="dropX"/>/<paramref name="dropY"/>
    /// are canvas-space coordinates snapped to grid by the caller.
    /// <paramref name="onCellAdded"/> is invoked (on UI thread) for each cell
    /// created so the caller can run highlight animations.
    /// <paramref name="downloadMedia"/> is invoked for URL-based cells that need
    /// async media download (implemented in MainWindow).
    /// </summary>
    Task ImportAsync(
        IDataTransfer data,
        double dropX,
        double dropY,
        Func<CellViewModel, Task> onCellAdded,
        Func<CellViewModel, string, Task> downloadMedia,
        CancellationToken ct = default);
}
