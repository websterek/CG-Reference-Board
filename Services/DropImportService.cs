using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

public sealed class DropImportService : IDropImportService
{
    private readonly MainWindowViewModel _vm;
    private readonly INotificationService _notifications;

    private static readonly string[] _imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".avif" };
    private static readonly string[] _videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
    private static readonly string[] _textExtensions = { ".txt", ".md", ".log", ".csv", ".json", ".xml" };

    private sealed record DropPayload(
        List<string> LocalPaths,
        List<string> WebUrls,
        string? PlainText,
        string? HtmlContent
    );

    public DropImportService(MainWindowViewModel vm, INotificationService notifications)
    {
        _vm = vm;
        _notifications = notifications;
    }

    public async Task ImportAsync(
        IDataTransfer data,
        double dropX,
        double dropY,
        Func<CellViewModel, Task> onCellAdded,
        Func<CellViewModel, string, Task> downloadMedia,
        CancellationToken ct = default)
    {
        double nextX = Math.Floor(dropX / Constants.GridSize) * Constants.GridSize;
        double nextY = Math.Floor(dropY / Constants.GridSize) * Constants.GridSize;

        // On Linux/Wayland DataTransfer implements IAsyncDataTransfer and data is
        // only accessible through async pipe reads.  Detect and use the async
        // collection path; fall back to the sync (Windows) path otherwise.
        DropPayload payload = data is IAsyncDataTransfer asyncTransfer
            ? await CollectDropPayloadAsync(asyncTransfer)
            : CollectDropPayload(data);

        int placedCount = 0;

        // ── Pass 1: local files (file-manager drops) ──────────────────────
        foreach (var path in payload.LocalPaths)
        {
            if (!File.Exists(path))
                continue;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool isImage = _imageExtensions.Contains(ext);
            bool isVideo = _videoExtensions.Contains(ext);
            bool isText = _textExtensions.Contains(ext);
            if (!isImage && !isVideo && !isText)
                continue;

            int colSpan = 2, rowSpan = 2;
            if (isImage)
            {
                var dim = GridLayoutService.GetImageDimensions(path);
                if (dim.HasValue)
                    (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dim.Value.Width, dim.Value.Height);
            }

            var space = GridLayoutService.FindEmptySpace(_vm.GridCells, nextX, nextY, colSpan, rowSpan, _vm.LayerManager.Items);
            if (space == null)
                continue;

            var cell = new CellViewModel
            {
                CanvasX = space.Value.X,
                CanvasY = space.Value.Y,
                ColSpan = colSpan,
                RowSpan = rowSpan
            };

            if (isVideo)
            {
                string destDir = Path.Combine(_vm.WorkspaceDir, "videos");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(path));
                if (path != destPath && !File.Exists(destPath))
                    File.Copy(path, destPath);
                string thumbDir = Path.Combine(_vm.WorkspaceDir, "images");
                string? thumb = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                cell.SetVideo(destPath, thumb ?? destPath);
            }
            else if (isText)
            {
                try
                { cell.SetText(File.ReadAllText(path)); }
                catch { continue; }
            }
            else
            {
                string destDir = Path.Combine(_vm.WorkspaceDir, "images");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(path));
                if (path != destPath && !File.Exists(destPath))
                    File.Copy(path, destPath);
                cell.SetImage(destPath);
            }

            _vm.GridCells.Add(cell);
            await onCellAdded(cell);
            placedCount++;
            nextX = space.Value.X + colSpan * Constants.GridSize;
        }

        if (placedCount > 0)
        {
            _vm.MarkUnsaved();
            _notifications.ShowToast($"📥 Dropped {placedCount} item(s)");
            return;
        }

        // ── Pass 2: web URLs (browser image / link drags) ─────────────────
        var webUrls = new List<string>(payload.WebUrls);
        if (payload.HtmlContent != null)
        {
            var imgSrc = TryExtractImageUrlFromHtml(payload.HtmlContent);
            if (imgSrc != null && !webUrls.Contains(imgSrc))
                webUrls.Insert(0, imgSrc);
        }

        foreach (var url in webUrls)
        {
            string urlPathStr;
            try
            { urlPathStr = new Uri(url).AbsolutePath; }
            catch { continue; }

            // Reserve a 2×2 slot; resized after image dimensions become known.
            var space = GridLayoutService.FindEmptySpace(_vm.GridCells, nextX, nextY, 2, 2, _vm.LayerManager.Items);
            if (space == null)
                continue;

            var cell = new CellViewModel
            {
                CanvasX = space.Value.X,
                CanvasY = space.Value.Y,
                ColSpan = 2,
                RowSpan = 2
            };

            _vm.GridCells.Add(cell);
            await onCellAdded(cell);
            await downloadMedia(cell, url);

            placedCount++;
            nextX = cell.CanvasX + cell.ColSpan * Constants.GridSize;
        }

        if (placedCount > 0)
        {
            _vm.MarkUnsaved();
            _notifications.ShowToast($"📥 Dropped {placedCount} item(s)");
            return;
        }

        // ── Pass 3: plain text ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(payload.PlainText))
        {
            var space = GridLayoutService.FindEmptySpace(_vm.GridCells, nextX, nextY, 2, 2, _vm.LayerManager.Items);
            if (space != null)
            {
                var cell = new CellViewModel
                {
                    CanvasX = space.Value.X,
                    CanvasY = space.Value.Y,
                    ColSpan = 2,
                    RowSpan = 2
                };
                cell.SetText(payload.PlainText.Trim());
                _vm.GridCells.Add(cell);
                await onCellAdded(cell);
                _vm.MarkUnsaved();
                _notifications.ShowToast("📥 Dropped text");
            }
            return;
        }

        // ── Pass 4: HTML stripped to readable plain text ──────────────────
        if (!string.IsNullOrWhiteSpace(payload.HtmlContent))
        {
            var stripped = Regex.Replace(payload.HtmlContent, "<[^>]+>", " ");
            stripped = System.Net.WebUtility.HtmlDecode(stripped);
            stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
            if (!string.IsNullOrEmpty(stripped))
            {
                var space = GridLayoutService.FindEmptySpace(_vm.GridCells, nextX, nextY, 2, 2, _vm.LayerManager.Items);
                if (space != null)
                {
                    var cell = new CellViewModel
                    {
                        CanvasX = space.Value.X,
                        CanvasY = space.Value.Y,
                        ColSpan = 2,
                        RowSpan = 2
                    };
                    cell.SetText(stripped);
                    _vm.GridCells.Add(cell);
                    await onCellAdded(cell);
                    _vm.MarkUnsaved();
                    _notifications.ShowToast("📥 Dropped text");
                }
            }
        }
    }

    private static async Task<DropPayload> CollectDropPayloadAsync(IAsyncDataTransfer data)
    {
        var localPaths = new List<string>();
        var webUrls = new List<string>();
        string? plainText = null;
        string? htmlContent = null;

        // ── 1. Avalonia typed file list ──────────────────────────────────
        var storageItems = await data.TryGetFilesAsync();
        if (storageItems != null)
        {
            foreach (var item in storageItems)
            {
                try
                {
                    var lp = item.Path.LocalPath;
                    if (!string.IsNullOrEmpty(lp))
                        localPaths.Add(lp);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectDropPayloadAsync: failed to read storage item path: {ex}");
                }
            }
        }

        // ── 2. text/uri-list  (RFC 2483) ────────────────────────────────
        var uriListFmt = DataFormat.CreateStringPlatformFormat("text/uri-list");
        var uriListText = await data.TryGetValueAsync(uriListFmt);
        if (!string.IsNullOrEmpty(uriListText))
        {
            foreach (var rawLine in uriListText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#'))
                    continue;
                try
                {
                    var uri = new Uri(line);
                    if (uri.IsFile)
                    {
                        var lp = uri.LocalPath;
                        if (!localPaths.Contains(lp))
                            localPaths.Add(lp);
                    }
                    else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        if (!webUrls.Contains(line))
                            webUrls.Add(line);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectDropPayloadAsync: URI-list processing error: {ex}");
                }
            }
        }

        // ── 3. text/x-moz-url  (Firefox) ────────────────────────────────
        var mozUrlFmt = DataFormat.CreateStringPlatformFormat("text/x-moz-url");
        var mozUrlText = await data.TryGetValueAsync(mozUrlFmt);
        if (!string.IsNullOrEmpty(mozUrlText))
        {
            var firstLine = mozUrlText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstLine))
            {
                try
                {
                    var uri = new Uri(firstLine);
                    if (uri.IsFile)
                    {
                        var lp = uri.LocalPath;
                        if (!localPaths.Contains(lp))
                            localPaths.Add(lp);
                    }
                    else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        if (!webUrls.Contains(firstLine))
                            webUrls.Add(firstLine);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectDropPayloadAsync: moz-url processing error: {ex}");
                }
            }
        }

        // ── 4. text/html ────────────────────────────────────────────────
        var htmlFmt = DataFormat.CreateStringPlatformFormat("text/html");
        var rawHtml = await data.TryGetValueAsync(htmlFmt);
        if (!string.IsNullOrWhiteSpace(rawHtml))
            htmlContent = rawHtml;

        // ── 5. text/plain ───────────────────────────────────────────────
        plainText = await data.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(plainText))
        {
            var plainFmt = DataFormat.CreateStringPlatformFormat("text/plain");
            plainText = await data.TryGetValueAsync(plainFmt);
        }
        if (string.IsNullOrWhiteSpace(plainText))
            plainText = null;

        if (plainText != null)
        {
            var trimmed = plainText.Trim();
            if (!trimmed.Contains('\n') && !trimmed.Contains(' ') &&
                (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                if (!webUrls.Contains(trimmed))
                    webUrls.Add(trimmed);
            }
        }

        return new DropPayload(localPaths, webUrls, plainText, htmlContent);
    }

    private static DropPayload CollectDropPayload(IDataObject data)
    {
        var localPaths = new List<string>();
        var webUrls = new List<string>();
        string? plainText = null;
        string? htmlContent = null;

        // Primary: Avalonia IStorageItem list (CF_HDROP on Windows).
        var storageItems = data.TryGetFiles();
        if (storageItems != null)
        {
            foreach (var item in storageItems)
            {
                try
                {
                    var lp = item.Path.LocalPath;
                    if (!string.IsNullOrEmpty(lp))
                        localPaths.Add(lp);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectDropPayload: failed to read storage item path: {ex}");
                }
            }
        }

        if (localPaths.Count == 0)
        {
            var uriListFmt = DataFormat.CreateStringPlatformFormat("text/uri-list");
            var uriListText = data.TryGetValue(uriListFmt);
            if (!string.IsNullOrEmpty(uriListText))
            {
                foreach (var rawLine in uriListText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = rawLine.Trim();
                    if (line.StartsWith('#'))
                        continue;
                    try
                    {
                        var uri = new Uri(line);
                        if (uri.IsFile)
                            localPaths.Add(uri.LocalPath);
                        else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                            webUrls.Add(line);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CollectDropPayload: failed to parse URI '{line}': {ex}");
                    }
                }
            }
        }

        var plainFmt = DataFormat.CreateStringPlatformFormat("text/plain");
        var rawText = data.TryGetValue(plainFmt);
        if (!string.IsNullOrWhiteSpace(rawText))
            plainText = rawText;

        if (plainText != null)
        {
            var trimmed = plainText.Trim();
            if (!trimmed.Contains('\n') && !trimmed.Contains(' ') &&
                (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                if (!webUrls.Contains(trimmed))
                    webUrls.Add(trimmed);
            }
        }

        var htmlFmt = DataFormat.CreateStringPlatformFormat("text/html");
        var rawHtml = data.TryGetValue(htmlFmt);
        if (!string.IsNullOrWhiteSpace(rawHtml))
            htmlContent = rawHtml;

        return new DropPayload(localPaths, webUrls, plainText, htmlContent);
    }

    private static string? TryExtractImageUrlFromHtml(string html)
    {
        var m = Regex.Match(html,
            @"<img\b[^>]*?\bsrc\s*=\s*[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!m.Success)
            return null;

        var src = m.Groups[1].Value.Trim();
        return (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            ? src
            : null;
    }
}
