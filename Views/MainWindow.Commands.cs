#pragma warning disable VSTHRD100 // XAML event handlers must be async void; see Tasks C2-C3

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Layers.Infrastructure;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
    #region Window Chrome Handlers

    private void TopBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Ignore double-taps that originate from inside the menu bar so that
        // double-clicking a menu header doesn't toggle the window state.
        if (IsSourceInsideMenu(e))
            return;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Do not begin a window drag when the press came from inside the Menu.
        // On Windows, BeginMoveDrag sends WM_NCLBUTTONDOWN/HTCAPTION to the OS
        // window manager, which captures the mouse and swallows subsequent
        // pointer events that should be delivered to the open menu popup.
        if (IsSourceInsideMenu(e))
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    /// <summary>
    /// Returns true when the event's source control is the <see cref="Menu"/>
    /// itself, any visual descendant of it (e.g. a MenuItem, PathIcon, TextBlock
    /// inside a menu header), or any MenuItem in a popup submenu. Popup content
    /// lives in a separate visual tree (PopupRoot) that does not contain the
    /// parent <see cref="Menu"/>, so we also match on <c>MenuItem</c> directly.
    /// </summary>
    private static bool IsSourceInsideMenu(RoutedEventArgs e)
    {
        var ctrl = e.Source as Visual;
        while (ctrl != null)
        {
            if (ctrl is Menu or MenuItem)
                return true;
            ctrl = ctrl.GetVisualParent();
        }
        return false;
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private async void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!await ConfirmDiscardChanges())
                return;
            Vm.ClosingConfirmed = true;
            Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CloseWindow_Click: {ex.Message}");
        }
    }

    private void TopLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.NorthWest, e);
    private void Top_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.North, e);
    private void TopRight_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.NorthEast, e);
    private void Right_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.East, e);
    private void BottomRight_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.SouthEast, e);
    private void Bottom_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.South, e);
    private void BottomLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.SouthWest, e);
    private void Left_PointerPressed(object? sender, PointerPressedEventArgs e) => this.BeginResizeDrag(WindowEdge.West, e);

    #endregion

    #region Menu Click Handlers

    private void Undo_Click(object? sender, RoutedEventArgs e) => Vm.UndoCommand.Execute(null);
    private void Redo_Click(object? sender, RoutedEventArgs e) => Vm.RedoCommand.Execute(null);
    private void GridMode_Click(object? sender, RoutedEventArgs e) => Vm.ModeService.SetMode("Grid");
    private void AnnotationMode_Click(object? sender, RoutedEventArgs e) => Vm.ModeService.SetMode("Annotation");
    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private async void SaveBoard_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var file = await topLevel!.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {Constants.AppName}",
            DefaultExtension = Constants.DefaultBoardExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType($"{Constants.AppName} Data")
                {
                    Patterns = new[] { $"*{Constants.DefaultBoardExtension}", "*.json" }
                }
            }
        });

        if (file == null)
            return;

        Vm.SetBoardFilePath(file.Path.LocalPath);

        Directory.CreateDirectory(Path.Combine(Vm.WorkspaceDir, "images"));
        Directory.CreateDirectory(Path.Combine(Vm.WorkspaceDir, "videos"));

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;

        _ = Vm.SaveBoardDataAsync();
        ShowToastAsync("💾 Saved");
    }

    private async void CreateDatabaseWizard_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChanges())
            return;

        var dialog = new CreateDatabaseWizardDialog();
        var result = await dialog.ShowDialog<bool>(this);

        if (result && !string.IsNullOrEmpty(dialog.BoardPath))
        {
            var boardPath = dialog.BoardPath;
            var workspaceDir = Path.GetDirectoryName(boardPath)!;

            Directory.CreateDirectory(Path.Combine(workspaceDir, "images"));
            Directory.CreateDirectory(Path.Combine(workspaceDir, "videos"));

            var emptyBoard = BoardSerializer.Serialize([], []);
            await File.WriteAllTextAsync(boardPath, emptyBoard);

            _ = Vm.LoadBoardFromFileAsync(boardPath);
            ShowToastAsync("💾 Database created");
        }
    }

    private async void LoadBoard_Click(object? sender, RoutedEventArgs e)
    {
        // #17: Warn before discarding the current board.
        if (!await ConfirmDiscardChanges())
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Open {Constants.AppName}",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType($"{Constants.AppName} Data")
                {
                    Patterns = new[] { $"*{Constants.DefaultBoardExtension}", "*.json" }
                }
            }
        });

        if (files is { Count: > 0 })
        {
            _ = Vm.LoadBoardFromFileAsync(files[0].Path.LocalPath);
            ShowToastAsync("📂 Opened");
        }
    }

    private async void NewBoardDialog_Click(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChanges())
            return;

        if (string.IsNullOrEmpty(Vm.WorkspaceDir) || !Directory.Exists(Vm.WorkspaceDir))
        {
            ShowToastAsync("⚠️ Open a board first to create new boards");
            return;
        }

        var dialog = new TextInputDialog();
        var result = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrEmpty(result))
        {
            var boardName = result;
            if (!boardName.EndsWith(Constants.DefaultBoardExtension))
                boardName += Constants.DefaultBoardExtension;

            var boardPath = Path.Combine(Vm.WorkspaceDir, boardName);

            if (File.Exists(boardPath))
            {
                ShowToastAsync("⚠️ Board already exists");
                return;
            }

            var emptyBoard = BoardSerializer.Serialize([], []);
            await File.WriteAllTextAsync(boardPath, emptyBoard);

            _ = Vm.LoadBoardFromFileAsync(boardPath);
            ShowToastAsync("📄 Board created");
        }
    }

    private void Home_Click(object? sender, RoutedEventArgs e)
    {
        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = true;
    }

    private async void NewBoard_Click(object? sender, RoutedEventArgs e)
    {
        // #17: warn about unsaved changes before wiping the board.
        if (!await ConfirmDiscardChanges())
            return;

        // #1: release all loaded bitmaps so they are not leaked.
        foreach (var cell in Vm.GridCells)
            cell.UnloadImage();
        ImageManager.ClearCaches();

        // #1: clear every piece of stale state before discarding view-models.
        ResetTransientPointerState(cancelActiveTransform: true);
        Vm.ResetInteractionState();
        _currentAnnotation = null;
        _editingTextAnnotation = null;

        Vm.GridCells.Clear();
        Vm.Annotations.Clear();

        Vm.CurrentBoardName = "New Board";
        Title = Constants.AppName;
        _ = Vm.UpdateBoardDirectoryListAsync();

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;

        ShowToastAsync("📄 New Board");
        ShowAll_Click(null, null!);
    }

    private async void ImportMedia_Click(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Import Media",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images & Videos")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp",
                                       "*.mp4", "*.webm", "*.avi", "*.mov", "*.mkv" }
                }
            }
        };

        var files = await this.StorageProvider.OpenFilePickerAsync(options);
        if (files == null || files.Count == 0)
            return;

        string[] videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
        int startX = 0, startY = 0;

        foreach (var file in files)
        {
            int x = startX, y = startY;

            while (Vm.GridCells.Any(c => (int)c.CanvasX == x && (int)c.CanvasY == y))
            {
                x += (int)Constants.GridSize;
                if (x > 1600)
                { x = startX; y += (int)Constants.GridSize; }
            }

            var cell = new CellViewModel { CanvasX = x, CanvasY = y };
            Vm.GridCells.Add(cell);
            HighlightCellAsync(cell);

            string ext = Path.GetExtension(file.Path.LocalPath).ToLowerInvariant();
            if (videoExtensions.Contains(ext))
            {
                string destDir = Path.Combine(Vm.WorkspaceDir, "videos");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(file.Path.LocalPath));
                if (file.Path.LocalPath != destPath && !File.Exists(destPath))
                    File.Copy(file.Path.LocalPath, destPath);

                // Try to extract a thumbnail frame via ffmpeg
                string thumbDir = Path.Combine(Vm.WorkspaceDir, "images");
                string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                cell.SetVideo(destPath, thumbPath ?? destPath);
                Vm.MarkUnsaved();
            }
            else
            {
                await LoadImageToCellAsync(cell, file.Path.LocalPath);
            }
        }
        ShowToastAsync("📥 Imported");
    }

    private void BoardDir_Click(object? sender, RoutedEventArgs e)
        => PlatformHelper.OpenDirectory(Vm.WorkspaceDir);

    private void BoardMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is MenuItem item && item.DataContext is BoardMenuItemViewModel vm)
        {
            var path = Path.Combine(Vm.WorkspaceDir, vm.FileName);
            if (File.Exists(path))
            {
                _ = Vm.LoadBoardFromFileAsync(path);
                ShowToastAsync("📂 Opened");
            }
        }
    }

    private void RecentBoard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is string path)
        {
            _ = Vm.LoadBoardFromFileAsync(path);
            ShowToastAsync("📂 Opened");
        }
    }

    #endregion

    #region Annotation Tool Mode Handlers

    private void BrushMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Brush"; }

    private void TextMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Text"; }

    private void ArrowMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Arrow"; }

    private void SquareMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Rectangle"; }

    private void CircleMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Ellipse"; }

    private void EraserMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Eraser"; }

    private void MoveMode_Click(object? sender, RoutedEventArgs e)
    { Vm.ModeService.AnnotationMode.CurrentTool = "Move"; }

    #endregion

    #region Cell Context Menu Handlers

    private async void CopyImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel { FilePath: not null } cell })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null || !File.Exists(cell.FilePath))
            return;

        try
        {
            using var stream = File.OpenRead(cell.FilePath);
            var bitmap = new Bitmap(stream);
            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetBitmap(bitmap);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
            ShowToastAsync("📋 Copied");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy image: {ex.Message}");
        }
    }

    private async void CopyText_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel cell } && !string.IsNullOrEmpty(cell.TextContent))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetText(cell.TextContent);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
            ShowToastAsync("📋 Copied");
        }
    }

    private async void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { FilePath: not null } cell })
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var dt = new DataTransfer();
            var item = new DataTransferItem();
            item.SetText(cell.FilePath);
            dt.Add(item);
            await clipboard.SetDataAsync(dt);
            ShowToastAsync("📋 Copied");
        }
    }

    private void ShowInExplorer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel cell })
            PlatformHelper.ShowInFileExplorer(cell.VideoPath ?? cell.FilePath ?? "");
    }

    private void OpenNative_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;

        string? pathToOpen = cell.IsImage ? cell.FilePath
                           : cell.IsVideo ? cell.VideoPath
                           : null;

        if (!string.IsNullOrEmpty(pathToOpen) && File.Exists(pathToOpen))
            PlatformHelper.OpenWithDefaultApp(pathToOpen);
    }

    private void EditText_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;
        if (!cell.IsText && !cell.IsBoardElement)
            return;

        FullImage.IsVisible = false;
        FullText.IsVisible = true;
        FullText.Text = cell.TextContent;
        _editingTextCell = cell;
        FullMediaOverlay.IsVisible = true;
    }

    private void ChangeColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;

        if (cell.IsBackdrop)
        {
            int idx = Array.IndexOf(Constants.BackdropBackgroundColors, cell.BackgroundColor);
            int next = (idx + 1) % Constants.BackdropBackgroundColors.Length;
            cell.BackgroundColor = Constants.BackdropBackgroundColors[next];
            cell.ForegroundColor = Constants.BackdropForegroundColors[next];
        }
        else if (cell.IsLabel)
        {
            int idx = Array.IndexOf(Constants.LabelForegroundColors, cell.ForegroundColor);
            cell.ForegroundColor = Constants.LabelForegroundColors[(idx + 1) % Constants.LabelForegroundColors.Length];
        }

        Vm.MarkUnsaved();
    }

    private void ToggleImageFit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsImage: true } cell })
        {
            cell.ImageStretch = cell.ImageStretch == "UniformToFill" ? "Uniform" : "UniformToFill";
            Vm.MarkUnsaved();
        }
    }

    private void IncreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell })
        {
            cell.FontSize += 8;
            Vm.MarkUnsaved();
        }
    }

    private void DecreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell } && cell.FontSize > 16)
        {
            cell.FontSize -= 8;
            Vm.MarkUnsaved();
        }
    }

    private void FitToContent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell })
            return;
        if (!cell.IsImage && !cell.IsVideo)
            return;
        if (string.IsNullOrEmpty(cell.FilePath))
            return;

        var dimensions = GridLayoutService.GetImageDimensions(cell.FilePath);
        if (dimensions == null)
            return;

        var (newColSpan, newRowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);

        if (!GridLayoutService.IsSpaceEmpty(Vm.GridCells, cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, Vm.LayerManager.ResolveLayer(cell)!, excludeCell: cell))
        {
            ShakeScreen();
            return;
        }

        cell.ColSpan = newColSpan;
        cell.RowSpan = newRowSpan;

        Vm.MarkUnsaved();
    }

    private void DeleteCell_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.IsViewMode)
            return;

        bool anyDeleted = DeleteSelectedContent(disposeCells: true);

        // Fallback: if nothing was selected, delete the right-clicked cell
        if (!anyDeleted && sender is MenuItem { DataContext: CellViewModel clickedCell })
        {
            clickedCell.Dispose();
            clickedCell.Clear();
            Vm.GridCells.Remove(clickedCell);
            anyDeleted = true;
        }

        if (anyDeleted)
        {
            Vm.MarkUnsaved();
            ShowToastAsync("🗑 Deleted");
        }
    }

    private bool DeleteSelectedContent(bool disposeCells)
    {
        bool anyDeleted = false;

        if (_selectedCells.Count > 0)
        {
            foreach (var cell in _selectedCells.ToList())
            {
                if (disposeCells)
                    cell.Dispose();

                cell.Clear();
                Vm.GridCells.Remove(cell);
            }

            _selectedCells.Clear();
            _hoveredCell = null;
            anyDeleted = true;
        }

        if (_selectedAnnotations.Count > 0)
        {
            foreach (var ann in _selectedAnnotations.ToList())
                Vm.Annotations.Remove(ann);

            _selectedAnnotations.Clear();
            anyDeleted = true;
        }

        if (anyDeleted)
            UpdateSelectionState();

        return anyDeleted;
    }

    #endregion

    #region Add Content (Context Menu)

    private void AddText_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.IsViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        // Check for collisions and find an empty slot, just like AddBackdrop does.
        Point? pos = GridLayoutService.IsSpaceEmpty(Vm.GridCells, x, y, 2, 2, Vm.LayerManager.Items)
            ? new Point(x, y)
            : GridLayoutService.FindEmptySpace(Vm.GridCells, x, y, 2, 2, Vm.LayerManager.Items);

        if (pos == null)
        {
            ShakeScreen();
            return;
        }

        var newCell = new CellViewModel { CanvasX = pos.Value.X, CanvasY = pos.Value.Y, ColSpan = 2, RowSpan = 2 };
        newCell.Type = CellType.Text;
        newCell.SetText("New Text Block");

        Vm.GridCells.Add(newCell);
        HighlightCellAsync(newCell);
        Vm.MarkUnsaved();
    }

    private void AddLabel_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.IsViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        // Labels use the Labels layer; check for space before placing.
        Point? pos = GridLayoutService.IsSpaceEmpty(Vm.GridCells, x, y, 4, 2, Vm.LayerManager.Labels)
            ? new Point(x, y)
            : GridLayoutService.FindEmptySpace(Vm.GridCells, x, y, 4, 2, Vm.LayerManager.Labels);

        if (pos == null)
        {
            ShakeScreen();
            return;
        }

        int colorIdx = Random.Shared.Next(Constants.BackdropBackgroundColors.Length);

        var newCell = new CellViewModel
        {
            CanvasX = pos.Value.X,
            CanvasY = pos.Value.Y,
            ColSpan = 4,
            RowSpan = 2,
            BackgroundColor = Constants.BackdropBackgroundColors[colorIdx],
            ForegroundColor = Constants.BackdropForegroundColors[colorIdx]
        };
        newCell.Type = CellType.Label;
        newCell.SetText("New Label");

        Vm.GridCells.Add(newCell);
        HighlightCellAsync(newCell);
        Vm.MarkUnsaved();
    }

    private void AddBackdrop_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm.IsViewMode)
            return;

        if (_selectedCells.Count > 0)
        {
            // Create backdrop around selected cells
            double minX = _selectedCells.Min(c => c.CanvasX);
            double minY = _selectedCells.Min(c => c.CanvasY);
            double maxX = _selectedCells.Max(c => c.CanvasX + c.PixelWidth);
            double maxY = _selectedCells.Max(c => c.CanvasY + c.PixelHeight);

            int gridX = (int)(Math.Floor(minX / Constants.GridSize) * Constants.GridSize);
            int gridY = (int)(Math.Floor(minY / Constants.GridSize) * Constants.GridSize);

            // Note: BackdropPadding is defined as half a grid cell (GridSize / 2).
            // The layout intentionally applies half-grid padding on both sides:
            // - Visual origin (VisualX/VisualY) is offset by BackdropPadding (left/top).
            // - PixelWidth/PixelHeight add a full GridSize, which equals 2 * BackdropPadding,
            //   so the backdrop extends half a grid outside the selected region on each side.
            double width = maxX - gridX + Constants.BackdropPadding;
            double height = maxY - gridY + Constants.BackdropPadding;

            int colSpan = (int)Math.Ceiling(width / Constants.GridSize);
            int rowSpan = (int)Math.Ceiling(height / Constants.GridSize);

            // Check for collision and find empty space if needed
            Point? finalPosition = null;
            if (GridLayoutService.IsSpaceEmpty(Vm.GridCells, gridX, gridY, colSpan, rowSpan, Vm.LayerManager.Backdrops))
            {
                finalPosition = new Point(gridX, gridY);
            }
            else
            {
                // Try to find nearby empty space
                finalPosition = GridLayoutService.FindEmptySpace(Vm.GridCells, gridX, gridY, colSpan, rowSpan, Vm.LayerManager.Backdrops);
            }

            if (finalPosition == null)
            {
                // Show feedback that no space is available
                ShakeScreen();
                return;
            }

            int colorIdx = Random.Shared.Next(Constants.BackdropBackgroundColors.Length);

            var backdrop = new CellViewModel
            {
                CanvasX = finalPosition.Value.X,
                CanvasY = finalPosition.Value.Y,
                ColSpan = colSpan,
                RowSpan = rowSpan,
                Type = CellType.Backdrop,
                TextContent = "Backdrop",
                BackgroundColor = Constants.BackdropBackgroundColors[colorIdx],
                ForegroundColor = Constants.BackdropForegroundColors[colorIdx]
            };

            Vm.GridCells.Add(backdrop);
            HighlightCellAsync(backdrop);
            Vm.MarkUnsaved();

            // Pan view to backdrop if it was placed in a different location
            if (Math.Abs(finalPosition.Value.X - gridX) > 1 || Math.Abs(finalPosition.Value.Y - gridY) > 1)
            {
                double centerX = finalPosition.Value.X + (colSpan * Constants.GridSize) / 2;
                double centerY = finalPosition.Value.Y + (rowSpan * Constants.GridSize) / 2;
                PanToPosition(centerX, centerY);
            }
        }
        else
        {
            // Manual placement mode - show preview and let user position it
            var hoverHighlight = this.FindControl<Border>("HoverHighlight");
            if (hoverHighlight == null)
                return;

            double x = Canvas.GetLeft(hoverHighlight);
            double y = Canvas.GetTop(hoverHighlight);

            // Snap to grid
            int gridX = (int)(Math.Floor(x / Constants.GridSize) * Constants.GridSize);
            int gridY = (int)(Math.Floor(y / Constants.GridSize) * Constants.GridSize);

            int colorIdx = Random.Shared.Next(Constants.BackdropBackgroundColors.Length);

            // Create pending backdrop
            _pendingBackdrop = new CellViewModel
            {
                ColSpan = 6,
                RowSpan = 4,
                Type = CellType.Backdrop,
                TextContent = "New Backdrop",
                BackgroundColor = Constants.BackdropBackgroundColors[colorIdx],
                ForegroundColor = Constants.BackdropForegroundColors[colorIdx]
            };

            // Show placement preview
            ShowPlacementPreview(gridX, gridY, _pendingBackdrop.ColSpan, _pendingBackdrop.RowSpan, Vm.LayerManager.Backdrops);
        }
    }

    private void SelectContent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell } || !cell.IsBackdrop)
            return;

        ClearSelection();

        var backdropBounds = TransformBoundsCalculator.GetCellBounds(cell);
        double left = backdropBounds.X;
        double top = backdropBounds.Y;
        double right = backdropBounds.Right;
        double bottom = backdropBounds.Bottom;

        foreach (var c in Vm.GridCells)
        {
            if (!c.HasContent)
                continue;

            double cx = c.CanvasX;
            double cy = c.CanvasY;
            double cw = c.ColSpan * Constants.GridSize;
            double ch = c.RowSpan * Constants.GridSize;

            bool intersects = cx < right && cx + cw > left
                           && cy < bottom && cy + ch > top;
            if (intersects)
            {
                c.IsSelected = true;
                _selectedCells.Add(c);
            }
        }

        foreach (var ann in Vm.Annotations)
        {
            bool inRect = AnnotationBoundsHelper.IntersectsRenderedGeometry(ann, new Rect(left, top, right - left, bottom - top));

            if (inRect)
            {
                ann.IsSelected = true;
                _selectedAnnotations.Add(ann);
            }
        }

        UpdateSelectionState();
    }

    private void ArrangeSelected_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);

        var sortedCells = _selectedCells.OrderBy(c => c.CanvasY).ThenBy(c => c.CanvasX).ToList();

        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);

        int itemsPerRow = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(sortedCells.Count)));
        double currentX = minX;
        double currentY = minY;
        double maxRowHeight = 0;
        int itemsInCurrentRow = 0;
        var cellsToAvoid = Vm.GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, currentX, currentY, cell.ColSpan, cell.RowSpan, Vm.LayerManager.ResolveLayer(cell)!);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
                cellsToAvoid.Add(cell);
            }

            maxRowHeight = Math.Max(maxRowHeight, cell.PixelHeight);
            currentX += cell.PixelWidth;
            itemsInCurrentRow++;

            if (itemsInCurrentRow >= itemsPerRow)
            {
                currentX = minX;
                currentY += maxRowHeight;
                maxRowHeight = 0;
                itemsInCurrentRow = 0;
            }
        }

        GridLayoutService.MoveAnnotationsWithCells(Vm.Annotations, oldPositions);
        Vm.MarkUnsaved();
    }

    private void ArrangeHorizontal_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);

        var sortedCells = _selectedCells.OrderBy(c => c.CanvasX).ThenBy(c => c.CanvasY).ToList();

        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);

        double currentX = minX;
        var cellsToAvoid = Vm.GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, currentX, minY, cell.ColSpan, cell.RowSpan, Vm.LayerManager.ResolveLayer(cell)!);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
                cellsToAvoid.Add(cell);
            }

            currentX += cell.PixelWidth;
        }

        GridLayoutService.MoveAnnotationsWithCells(Vm.Annotations, oldPositions);
        Vm.MarkUnsaved();
    }

    private void ArrangeVertical_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCells.Count == 0)
            return;

        double minX = _selectedCells.Min(c => c.CanvasX);
        double minY = _selectedCells.Min(c => c.CanvasY);

        var sortedCells = _selectedCells.OrderBy(c => c.CanvasY).ThenBy(c => c.CanvasX).ToList();

        var oldPositions = new Dictionary<CellViewModel, Point>();
        foreach (var cell in sortedCells)
            oldPositions[cell] = new Point(cell.CanvasX, cell.CanvasY);

        double currentY = minY;
        var cellsToAvoid = Vm.GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, minX, currentY, cell.ColSpan, cell.RowSpan, Vm.LayerManager.ResolveLayer(cell)!);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
                cellsToAvoid.Add(cell);
            }

            currentY += cell.PixelHeight;
        }

        GridLayoutService.MoveAnnotationsWithCells(Vm.Annotations, oldPositions);
        Vm.MarkUnsaved();
    }

    #endregion

    #region Drag & Drop

    // ── File-type sets shared across all drop and paste paths ─────────────

    private static readonly string[] _imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".avif" };
    private static readonly string[] _videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
    private static readonly string[] _textExtensions = { ".txt", ".md", ".log", ".csv", ".json", ".xml" };

    /// <summary>
    /// All data types that may arrive in a single drag-and-drop transfer.
    /// <list type="bullet">
    ///   <item><b>LocalPaths</b>  — absolute paths from file-manager drops.</item>
    ///   <item><b>WebUrls</b>     — http/https URLs from browser or link drags.</item>
    ///   <item><b>PlainText</b>   — raw text from any source.</item>
    ///   <item><b>HtmlContent</b> — HTML fragment (browser image / selection drag).</item>
    /// </list>
    /// </summary>
    private sealed record DropPayload(
        List<string> LocalPaths,
        List<string> WebUrls,
        string? PlainText,
        string? HtmlContent
    );

    /// <summary>
    /// Collects all useful data from an async DnD transfer (Linux / Wayland path).
    /// <para>
    /// On Wayland the compositor hands data only via async pipe reads — the
    /// synchronous <c>TryGetFiles()</c> / <c>TryGetValue()</c> wrappers always
    /// return null there.  Every known MIME type is therefore read asynchronously
    /// so that file-manager drops, browser image drags, and bare-URL drops all work.
    /// </para>
    /// </summary>
    private static async Task<DropPayload> CollectDropPayloadAsync(IAsyncDataTransfer data)
    {
        var localPaths = new List<string>();
        var webUrls = new List<string>();
        string? plainText = null;
        string? htmlContent = null;

        // ── 1. Avalonia typed file list ──────────────────────────────────
        // Works on X11 and on Wayland when the backend can automatically map
        // the platform format to an IStorageItem sequence.
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
        // Dolphin, Nautilus, Thunar and most GTK/Qt file managers deliver
        // dragged files via this MIME type over both XDND (X11) and the
        // xdg-desktop-portal / Wayland DnD protocol.
        // Browser drags also put the image or page URL here.
        var uriListFmt = DataFormat.CreateStringPlatformFormat("text/uri-list");
        var uriListText = await data.TryGetValueAsync(uriListFmt);
        if (!string.IsNullOrEmpty(uriListText))
        {
            foreach (var rawLine in uriListText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#'))
                    continue; // RFC 2483 comment
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
        // Firefox encodes dragged links and images as "URL\r\nTitle" in this
        // proprietary type — it is the highest-fidelity source for Firefox
        // image drags on Linux.
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
        // Chromium and Firefox populate this when dragging images or selected
        // page content.  Stored for later <img src="…"> extraction.
        var htmlFmt = DataFormat.CreateStringPlatformFormat("text/html");
        var rawHtml = await data.TryGetValueAsync(htmlFmt);
        if (!string.IsNullOrWhiteSpace(rawHtml))
            htmlContent = rawHtml;

        // ── 5. text/plain ───────────────────────────────────────────────
        // Plain-text fallback — could be a bare URL or free-form text.
        plainText = await data.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(plainText))
        {
            var plainFmt = DataFormat.CreateStringPlatformFormat("text/plain");
            plainText = await data.TryGetValueAsync(plainFmt);
        }
        if (string.IsNullOrWhiteSpace(plainText))
            plainText = null;

        // Promote a single bare URL in plain text to the webUrls list.
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

    /// <summary>
    /// Collects all useful data from a synchronous DnD transfer (Windows path).
    /// Windows uses COM <c>IDataObject</c> with <c>CF_HDROP</c> for files, so
    /// the Avalonia <c>TryGetFiles()</c> path works reliably there.
    /// </summary>
    private static DropPayload CollectDropPayload(IDataTransfer data)
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
            // text/uri-list fallback (some Windows apps also use this format).
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

        // text/plain — might be a URL or free-form text.
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

        // text/html — browser image / selection drags.
        var htmlFmt = DataFormat.CreateStringPlatformFormat("text/html");
        var rawHtml = data.TryGetValue(htmlFmt);
        if (!string.IsNullOrWhiteSpace(rawHtml))
            htmlContent = rawHtml;

        return new DropPayload(localPaths, webUrls, plainText, htmlContent);
    }

    /// <summary>
    /// Extracts the first absolute http/https <c>src</c> URL from an &lt;img&gt; tag
    /// inside an HTML fragment (e.g. the <c>text/html</c> payload from a browser drag).
    /// Returns <c>null</c> when no such URL is found.
    /// </summary>
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

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // Must explicitly accept the drag during DragEnter on Linux/Wayland.
        // Without this, the compositor treats the window as a non-target and
        // stops delivering DragOver and Drop events entirely.
        e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        e.Handled = true;

        // Switch to grid mode when dragging files from system
        if (Vm.IsDrawMode)
            Vm.ModeService.SetMode("Grid");
        _isDraggingFromSystem = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        e.Handled = true;

        if (!_isDraggingFromSystem || Vm.IsViewMode)
            return;

        var dropPt = e.GetPosition(MainCanvas);
        int gridX = (int)(Math.Floor(dropPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(dropPt.Y / Constants.GridSize) * Constants.GridSize);

        // Get first file path to determine preview size
        int colSpan = 2, rowSpan = 2;
        var storageItems = e.DataTransfer.TryGetFiles();
        if (storageItems != null)
        {
            try
            {
                var firstItem = storageItems.FirstOrDefault();
                if (firstItem != null)
                {
                    var filePath = firstItem.Path.LocalPath;
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        var ext = Path.GetExtension(filePath).ToLowerInvariant();
                        if (_imageExtensions.Contains(ext))
                        {
                            var dim = GridLayoutService.GetImageDimensions(filePath);
                            if (dim.HasValue)
                                (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dim.Value.Width, dim.Value.Height);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnDragOver: failed to probe first storage item for dimensions: {ex}");
            }
        }

        // Find snap position
        var space = GridLayoutService.FindEmptySpace(Vm.GridCells, gridX, gridY, colSpan, rowSpan, Vm.LayerManager.Items);
        if (space == null)
            return;

        var preview = this.FindControl<Border>("DropPreview");
        if (preview != null)
        {
            Canvas.SetLeft(preview, space.Value.X);
            Canvas.SetTop(preview, space.Value.Y);
            preview.Width = colSpan * Constants.GridSize;
            preview.Height = rowSpan * Constants.GridSize;
            preview.IsVisible = true;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _isDraggingFromSystem = false;
        var preview = this.FindControl<Border>("DropPreview");
        if (preview != null)
            preview.IsVisible = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm.IsViewMode)
        { e.Handled = true; return; }
        e.Handled = true;

        // Hide preview and reset flag
        _isDraggingFromSystem = false;
        var preview = this.FindControl<Border>("DropPreview");
        if (preview != null)
            preview.IsVisible = false;

        var dropPt = e.GetPosition(MainCanvas);
        double nextX = Math.Floor(dropPt.X / Constants.GridSize) * Constants.GridSize;
        double nextY = Math.Floor(dropPt.Y / Constants.GridSize) * Constants.GridSize;

        // On Linux/Wayland DataTransfer implements IAsyncDataTransfer and data is
        // only accessible through async pipe reads.  Detect and use the async
        // collection path; fall back to the sync (Windows) path otherwise.
        DropPayload payload = e.DataTransfer is IAsyncDataTransfer asyncTransfer
            ? await CollectDropPayloadAsync(asyncTransfer)
            : CollectDropPayload(e.DataTransfer);

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

            var space = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, colSpan, rowSpan, Vm.LayerManager.Items);
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
                string destDir = Path.Combine(Vm.WorkspaceDir, "videos");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(path));
                if (path != destPath && !File.Exists(destPath))
                    File.Copy(path, destPath);
                string thumbDir = Path.Combine(Vm.WorkspaceDir, "images");
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
                string destDir = Path.Combine(Vm.WorkspaceDir, "images");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(path));
                if (path != destPath && !File.Exists(destPath))
                    File.Copy(path, destPath);
                cell.SetImage(destPath);
            }

            Vm.GridCells.Add(cell);
            HighlightCellAsync(cell);
            placedCount++;
            nextX = space.Value.X + colSpan * Constants.GridSize;
        }

        if (placedCount > 0)
        {
            Vm.MarkUnsaved();
            ShowToastAsync($"📥 Dropped {placedCount} item(s)");
            return;
        }

        // ── Pass 2: web URLs (browser image / link drags) ─────────────────
        // Also check the HTML payload for an <img src="…"> URL — Chromium omits
        // direct image URLs from uri-list for cross-origin images, but always
        // provides them in the text/html fragment.
        var webUrls = new List<string>(payload.WebUrls);
        if (payload.HtmlContent != null)
        {
            var imgSrc = TryExtractImageUrlFromHtml(payload.HtmlContent);
            if (imgSrc != null && !webUrls.Contains(imgSrc))
                webUrls.Insert(0, imgSrc); // prefer the direct img src
        }

        foreach (var url in webUrls)
        {
            string urlPathStr;
            try
            { urlPathStr = new Uri(url).AbsolutePath; }
            catch { continue; }

            string urlExt = Path.GetExtension(urlPathStr).ToLowerInvariant();
            bool isVideoUrl = _videoExtensions.Contains(urlExt)
                             || url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase)
                             || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
                             || url.Contains("vimeo.com", StringComparison.OrdinalIgnoreCase);

            // Reserve a 2×2 slot; resized after image dimensions become known.
            var space = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, 2, 2, Vm.LayerManager.Items);
            if (space == null)
                continue;

            var cell = new CellViewModel
            {
                CanvasX = space.Value.X,
                CanvasY = space.Value.Y,
                ColSpan = 2,
                RowSpan = 2
            };

            if (isVideoUrl)
            {
                Vm.GridCells.Add(cell);
                HighlightCellAsync(cell);
                await DownloadMediaToCell(cell, url);
            }
            else
            {
                Vm.GridCells.Add(cell);
                HighlightCellAsync(cell);
                await DownloadMediaToCell(cell, url);
            }

            placedCount++;
            nextX = cell.CanvasX + cell.ColSpan * Constants.GridSize;
        }

        if (placedCount > 0)
        {
            Vm.MarkUnsaved();
            ShowToastAsync($"📥 Dropped {placedCount} item(s)");
            return;
        }

        // ── Pass 3: plain text ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(payload.PlainText))
        {
            var space = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, 2, 2, Vm.LayerManager.Items);
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
                Vm.GridCells.Add(cell);
                HighlightCellAsync(cell);
                Vm.MarkUnsaved();
                ShowToastAsync("📥 Dropped text");
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
                var space = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, 2, 2, Vm.LayerManager.Items);
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
                    Vm.GridCells.Add(cell);
                    HighlightCellAsync(cell);
                    Vm.MarkUnsaved();
                    ShowToastAsync("📥 Dropped text");
                }
            }
        }
    }

    #endregion

    #region Keyboard Shortcuts

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (FullMediaOverlay.IsVisible || (startupOverlay?.IsVisible == true))
            return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool noModifiers = e.KeyModifiers == KeyModifiers.None;

        // Cancel placement preview on Escape
        if (e.Key == Key.Escape && _isShowingPlacementPreview)
        {
            HidePlacementPreview();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && HandleEscapeShortcut())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N && isCtrl)
        { NewBoard_Click(null, null!); return; }

        if (e.Key == Key.O && isCtrl)
        { LoadBoard_Click(null, null!); return; }

        if (e.Key == Key.S && isCtrl)
        {
            if (!string.IsNullOrEmpty(Vm.CurrentBoardFile))
            {
                _ = Vm.SaveBoardDataAsync();
                ShowToastAsync("💾 Saved");
            }
            else
                SaveBoard_Click(null, null!);
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox { IsVisible: true })
            return;

        if (e.Key == Key.Z && isCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        { Vm.RedoCommand.Execute(null); return; }
        if (e.Key == Key.Y && isCtrl)
        { Vm.RedoCommand.Execute(null); return; }

        if (e.Key == Key.Z && isCtrl)
        { Vm.UndoCommand.Execute(null); return; }

        if (e.Key == Key.I && isCtrl)
        { ImportMedia_Click(null, null!); return; }

        // Ctrl+Shift+C: Copy image to clipboard
        if (e.Key == Key.C && isCtrl && isShift)
        {
            var targetCell = _selectedCells.FirstOrDefault(c => c.IsImage || c.IsVideo);
            if (targetCell != null && !string.IsNullOrEmpty(targetCell.FilePath) && File.Exists(targetCell.FilePath))
            {
                try
                {
                    using var stream = File.OpenRead(targetCell.FilePath);
                    var bmp = new Avalonia.Media.Imaging.Bitmap(stream);
                    var dt = new DataTransfer();
                    var item = new DataTransferItem();
                    item.SetBitmap(bmp);
                    dt.Add(item);
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null)
                        await clipboard.SetDataAsync(dt);
                    ShowToastAsync("📋 Image copied");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Window_KeyDown: failed to copy image to clipboard: {ex}");
                }
            }
            return;
        }

        // Ctrl+C: Copy path of selected image/video, or text content
        if (e.Key == Key.C && isCtrl && !isShift)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            // Prefer file path for image/video cells
            var fileCell = _selectedCells.FirstOrDefault(c => c.IsFile && !string.IsNullOrEmpty(c.FilePath));
            if (fileCell != null)
            {
                var dt = new DataTransfer();
                var item = new DataTransferItem();
                item.SetText(fileCell.FilePath!);
                dt.Add(item);
                await clipboard.SetDataAsync(dt);
                ShowToastAsync("📋 Path copied");
                return;
            }

            // Fall back to text content
            var textCell = _selectedCells.FirstOrDefault(c => c.HasTextContent && !string.IsNullOrEmpty(c.TextContent));
            if (textCell != null)
            {
                var dt = new DataTransfer();
                var item = new DataTransferItem();
                item.SetText(textCell.TextContent!);
                dt.Add(item);
                await clipboard.SetDataAsync(dt);
                ShowToastAsync("📋 Text copied");
                return;
            }
            return;
        }

        if (e.Key == Key.V && isCtrl)
        {
            if (Vm.IsViewMode)
                return;

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                return;

            var data = await clipboard.TryGetDataAsync();
            if (data == null)
                return;

            double preferredX, preferredY;
            if (_hoveredCell is { IsBoardElement: true } || _hoveredCell == null)
            {
                var hoverHighlight = this.FindControl<Border>("HoverHighlight");
                if (hoverHighlight != null && hoverHighlight.IsVisible)
                {
                    double left = Canvas.GetLeft(hoverHighlight);
                    double top = Canvas.GetTop(hoverHighlight);
                    if (!double.IsNaN(left) && !double.IsNaN(top))
                    {
                        preferredX = left + 80;
                        preferredY = top + 80;
                    }
                    else
                    {
                        preferredX = Bounds.Width / 2 / _scale.ScaleX - _translate.X;
                        preferredY = Bounds.Height / 2 / _scale.ScaleY - _translate.Y;
                    }
                }
                else
                {
                    preferredX = Bounds.Width / 2 / _scale.ScaleX - _translate.X;
                    preferredY = Bounds.Height / 2 / _scale.ScaleY - _translate.Y;
                }
            }
            else
            {
                preferredX = _hoveredCell.CanvasX;
                preferredY = _hoveredCell.CanvasY;
            }

            var text = await data.TryGetTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                // If the pasted text is a file:// URI or one or more absolute file paths,
                // treat it like a file import (copy into workspace and create cells).
                var lines = text.Trim()
                                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrEmpty(l))
                                .ToList();

                var filePaths = new List<string>();
                foreach (var line in lines)
                {
                    if (line.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var uri = new Uri(line);
                            if (uri.IsFile)
                            {
                                var lp = uri.LocalPath;
                                if (!string.IsNullOrEmpty(lp) && File.Exists(lp))
                                    filePaths.Add(lp);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Window_KeyDown: failed to parse URI '{line}': {ex}");
                        }
                    }
                    else
                    {
                        try
                        {
                            if (Path.IsPathRooted(line) && File.Exists(line))
                                filePaths.Add(line);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Window_KeyDown: failed to handle file path '{line}': {ex}");
                        }
                    }
                }

                if (filePaths.Count > 0)
                {
                    double nextX = preferredX;
                    double nextY = preferredY;
                    var pastedCells = new List<CellViewModel>();

                    foreach (var filePath in filePaths)
                    {
                        string ext = Path.GetExtension(filePath).ToLowerInvariant();
                        bool isImage = _imageExtensions.Contains(ext);
                        bool isVideo = _videoExtensions.Contains(ext);
                        bool isText = _textExtensions.Contains(ext);

                        if (!isImage && !isVideo && !isText)
                            continue; // skip unsupported — don't abort the whole batch

                        try
                        {
                            int colSpan = 2, rowSpan = 2;
                            if (isImage)
                            {
                                var dimensions = GridLayoutService.GetImageDimensions(filePath);
                                if (dimensions != null)
                                    (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                            }

                            Point? emptySpace = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, colSpan, rowSpan, Vm.LayerManager.Items);
                            if (emptySpace == null)
                                continue; // no room — skip this file

                            var newCell = new CellViewModel
                            {
                                CanvasX = emptySpace.Value.X,
                                CanvasY = emptySpace.Value.Y,
                                ColSpan = colSpan,
                                RowSpan = rowSpan
                            };

                            if (isVideo)
                            {
                                string destDir = Path.Combine(Vm.WorkspaceDir, "videos");
                                Directory.CreateDirectory(destDir);
                                string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                                if (filePath != destPath && !File.Exists(destPath))
                                    File.Copy(filePath, destPath);

                                string thumbDir = Path.Combine(Vm.WorkspaceDir, "images");
                                string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                                newCell.SetVideo(destPath, thumbPath ?? destPath);
                            }
                            else if (isText)
                            {
                                newCell.SetText(File.ReadAllText(filePath));
                            }
                            else
                            {
                                string destDir = Path.Combine(Vm.WorkspaceDir, "images");
                                Directory.CreateDirectory(destDir);
                                string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                                if (filePath != destPath && !File.Exists(destPath))
                                    File.Copy(filePath, destPath);
                                newCell.SetImage(destPath);
                            }

                            if (!newCell.HasContent)
                                continue; // corrupt / unreadable file

                            Vm.GridCells.Add(newCell);
                            HighlightCellAsync(newCell);
                            pastedCells.Add(newCell);

                            // Advance the preferred origin so the next file lands to the right.
                            nextX = emptySpace.Value.X + colSpan * Constants.GridSize;
                        }
                        catch { /* skip unreadable files silently */ }
                    }

                    if (pastedCells.Count == 0)
                    {
                        ShowToastAsync("⚠️ No supported files to paste");
                        return;
                    }

                    // Select all pasted cells and pan to the first one.
                    ClearSelection();
                    foreach (var c in pastedCells)
                    {
                        c.IsSelected = true;
                        _selectedCells.Add(c);
                    }
                    UpdateSelectionState();
                    PanToPosition(
                        pastedCells[0].CanvasX + pastedCells[0].ColSpan * Constants.GridSize / 2.0,
                        pastedCells[0].CanvasY + pastedCells[0].RowSpan * Constants.GridSize / 2.0);

                    Vm.MarkUnsaved();
                    ShowToastAsync(pastedCells.Count == 1 ? "📋 Pasted" : $"📋 Pasted {pastedCells.Count} items");
                    return;
                }

                // Not file paths — fall back to URL or plain text handling.
                var single = text.Trim();
                if (single.Contains("youtube.com") || single.Contains("youtu.be") || single.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    Point? emptySpace = GridLayoutService.FindEmptySpace(Vm.GridCells, preferredX, preferredY, 2, 2, Vm.LayerManager.Items);
                    if (emptySpace == null)
                    {
                        ShakeScreen();
                        return;
                    }

                    var newCell = new CellViewModel
                    {
                        CanvasX = emptySpace.Value.X,
                        CanvasY = emptySpace.Value.Y,
                        ColSpan = 2,
                        RowSpan = 2
                    };
                    newCell.SetText($"Checking availability...\n{single}");
                    Vm.GridCells.Add(newCell);
                    SelectAndPanToCell(newCell);

                    await DownloadMediaToCell(newCell, single);

                    HighlightCellAsync(newCell);
                    Vm.MarkUnsaved();
                    ShowToastAsync("📋 Pasted");
                    return;
                }
                else
                {
                    Point? emptySpace = GridLayoutService.FindEmptySpace(Vm.GridCells, preferredX, preferredY, 2, 2, Vm.LayerManager.Items);
                    if (emptySpace == null)
                    {
                        ShakeScreen();
                        return;
                    }

                    var newCell = new CellViewModel
                    {
                        CanvasX = emptySpace.Value.X,
                        CanvasY = emptySpace.Value.Y,
                        ColSpan = 2,
                        RowSpan = 2
                    };
                    newCell.SetText(single);
                    Vm.GridCells.Add(newCell);
                    SelectAndPanToCell(newCell);

                    HighlightCellAsync(newCell);
                    Vm.MarkUnsaved();
                    ShowToastAsync("📋 Pasted");
                    return;
                }
            }

            var pastedFiles = await data.TryGetFilesAsync();
            if (pastedFiles != null && pastedFiles.Any())
            {
                // Collect all file paths up-front so we can iterate them.
                var filePaths = pastedFiles
                    .Select(f => { try { return f.Path.LocalPath; } catch { return null; } })
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .ToList();

                double nextX = preferredX;
                double nextY = preferredY;
                var pastedCells = new List<CellViewModel>();

                foreach (var filePath in filePaths)
                {
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    bool isImage = _imageExtensions.Contains(ext);
                    bool isVideo = _videoExtensions.Contains(ext);
                    bool isText = _textExtensions.Contains(ext);

                    if (!isImage && !isVideo && !isText)
                        continue; // skip unsupported — don't abort the whole batch

                    try
                    {
                        int colSpan = 2, rowSpan = 2;
                        if (isImage)
                        {
                            var dimensions = GridLayoutService.GetImageDimensions(filePath);
                            if (dimensions != null)
                                (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                        }

                        Point? emptySpace = GridLayoutService.FindEmptySpace(Vm.GridCells, nextX, nextY, colSpan, rowSpan, Vm.LayerManager.Items);
                        if (emptySpace == null)
                            continue; // no room — skip this file

                        var newCell = new CellViewModel
                        {
                            CanvasX = emptySpace.Value.X,
                            CanvasY = emptySpace.Value.Y,
                            ColSpan = colSpan,
                            RowSpan = rowSpan
                        };

                        if (isVideo)
                        {
                            string destDir = Path.Combine(Vm.WorkspaceDir, "videos");
                            Directory.CreateDirectory(destDir);
                            string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                            if (filePath != destPath && !File.Exists(destPath))
                                File.Copy(filePath, destPath);

                            string thumbDir = Path.Combine(Vm.WorkspaceDir, "images");
                            string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                            newCell.SetVideo(destPath, thumbPath ?? destPath);
                        }
                        else if (isText)
                        {
                            newCell.SetText(File.ReadAllText(filePath));
                        }
                        else
                        {
                            string destDir = Path.Combine(Vm.WorkspaceDir, "images");
                            Directory.CreateDirectory(destDir);
                            string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                            if (filePath != destPath && !File.Exists(destPath))
                                File.Copy(filePath, destPath);
                            newCell.SetImage(destPath);
                        }

                        if (!newCell.HasContent)
                            continue; // corrupt / unreadable file

                        Vm.GridCells.Add(newCell);
                        HighlightCellAsync(newCell);
                        pastedCells.Add(newCell);

                        // Advance the preferred origin so the next file lands to the right.
                        nextX = emptySpace.Value.X + colSpan * Constants.GridSize;
                    }
                    catch { /* skip unreadable files silently */ }
                }

                if (pastedCells.Count == 0)
                {
                    ShowToastAsync("⚠️ No supported files to paste");
                    return;
                }

                // Select all pasted cells and pan to the first one.
                ClearSelection();
                foreach (var c in pastedCells)
                {
                    c.IsSelected = true;
                    _selectedCells.Add(c);
                }
                UpdateSelectionState();
                PanToPosition(
                    pastedCells[0].CanvasX + pastedCells[0].ColSpan * Constants.GridSize / 2.0,
                    pastedCells[0].CanvasY + pastedCells[0].RowSpan * Constants.GridSize / 2.0);

                Vm.MarkUnsaved();
                ShowToastAsync(pastedCells.Count == 1 ? "📋 Pasted" : $"📋 Pasted {pastedCells.Count} items");
                return;
            }

            Avalonia.Media.Imaging.Bitmap? bitmap = null;
            try
            { bitmap = await data.TryGetBitmapAsync(); }
            catch { /* X11 clipboard may throw when data is not a valid bitmap */ }
            if (bitmap != null)
            {
                string destDir = Path.Combine(Vm.WorkspaceDir, "images");
                Directory.CreateDirectory(destDir);
                string path = Path.Combine(destDir, Guid.NewGuid() + ".png");
                bitmap.Save(path);

                var dimensions = GridLayoutService.GetImageDimensions(path);
                var (colSpan, rowSpan) = dimensions != null
                    ? GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height)
                    : (2, 2);

                Point? emptySpace = GridLayoutService.FindEmptySpace(Vm.GridCells, preferredX, preferredY, colSpan, rowSpan, Vm.LayerManager.Items);

                if (emptySpace == null)
                {
                    ShakeScreen();
                    return;
                }

                var newCell = new CellViewModel
                {
                    CanvasX = emptySpace.Value.X,
                    CanvasY = emptySpace.Value.Y,
                    ColSpan = colSpan,
                    RowSpan = rowSpan
                };
                newCell.SetImage(path);
                Vm.GridCells.Add(newCell);
                SelectAndPanToCell(newCell);
                HighlightCellAsync(newCell);
                Vm.MarkUnsaved();
                ShowToastAsync("📋 Pasted");
                return;
            }

            Vm.RequestSave();
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearSelection();
            return;
        }

        if (e.Key == Key.Home && noModifiers)
        { ZoomReset_Click(null, null!); return; }

        if (e.Key == Key.D1 && isCtrl)
        { Vm.ModeService.SetMode("Grid"); return; }

        if (e.Key == Key.D2 && isCtrl)
        { Vm.ModeService.SetMode("Annotation"); return; }

        if (e.Key == Key.A && isShift && !isCtrl)
        { Vm.IsAnnotationsVisible = !Vm.IsAnnotationsVisible; return; }

        // Annotation tool shortcuts (Photoshop-style)
        if (Vm.IsDrawMode && noModifiers)
        {
            switch (e.Key)
            {
                case Key.B:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Brush";
                    ShowToastAsync("🖌️ Brush");
                    return;
                case Key.E:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Eraser";
                    ShowToastAsync("🧹 Eraser");
                    return;
                case Key.T:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Text";
                    ShowToastAsync("🔤 Text");
                    return;
                case Key.L:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Arrow";
                    ShowToastAsync("➡️ Arrow");
                    return;
                case Key.U:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Rectangle";
                    ShowToastAsync("▪️ Rectangle");
                    return;
                case Key.O:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Ellipse";
                    ShowToastAsync("⚪ Ellipse");
                    return;
                case Key.V:
                    Vm.ModeService.AnnotationMode.CurrentTool = "Move";
                    ShowToastAsync("✥ Select/Move");
                    return;
            }
        }



        if (e.Key == Key.F && noModifiers)
        { ShowAll_Click(null, null!); return; }

        if (e.Key == Key.F && isShift && !isCtrl)
        { ShowSelected_Click(null, null!); return; }

        if (e.Key == Key.F && isCtrl && isShift)
        {
            if (_selectedCells.Count > 0)
            {
                foreach (var cell in _selectedCells.ToList())
                {
                    if (!cell.IsImage && !cell.IsVideo)
                        continue;
                    if (string.IsNullOrEmpty(cell.FilePath))
                        continue;

                    var dimensions = GridLayoutService.GetImageDimensions(cell.FilePath);
                    if (dimensions == null)
                        continue;

                    var (newColSpan, newRowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);

                    if (GridLayoutService.IsSpaceEmpty(Vm.GridCells, cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, Vm.LayerManager.ResolveLayer(cell)!, excludeCell: cell))
                    {
                        cell.ColSpan = newColSpan;
                        cell.RowSpan = newRowSpan;
                    }
                }

                Vm.MarkUnsaved();
            }
            return;
        }

        if (e.Key == Key.T && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            Vm.IsAlwaysOnTop = !Vm.IsAlwaysOnTop;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (Vm.IsViewMode)
                return;

            bool anyDeleted = DeleteSelectedContent(disposeCells: false);

            if (anyDeleted)
            {
                Vm.MarkUnsaved();
                ShowToastAsync("🗑 Deleted");
            }
            else if (_hoveredCell != null)
            {
                _hoveredCell.Clear();
                Vm.GridCells.Remove(_hoveredCell);
                _hoveredCell = null;
                Vm.MarkUnsaved();
                ShowToastAsync("🗑 Deleted");
            }
        }
    }

    #endregion

    #region Fullscreen Media Overlay

    private void CanvasImage_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CellViewModel cell })
            return;

        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (cell.IsVideo)
        {
            if (isShift)
            {
                // Shift+double-click: Open in system video player
                string? videoPath = cell.VideoPath;
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                    PlatformHelper.OpenWithDefaultApp(videoPath);
            }
            else
            {
                // Normal double-click: Zoom to fill screen completely
                ClearSelection();
                cell.IsSelected = true;
                _selectedCells.Add(cell);
                UpdateSelectionState();
                ZoomToCell(cell);
            }
        }
        else if (cell.IsImage)
        {
            if (isShift)
            {
                // Shift+double-click: Open in system image viewer
                string? imagePath = cell.FilePath;
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    PlatformHelper.OpenWithDefaultApp(imagePath);
            }
            else
            {
                // Normal double-click: Zoom to fill screen completely
                ClearSelection();
                cell.IsSelected = true;
                _selectedCells.Add(cell);
                UpdateSelectionState();
                ZoomToCell(cell);
            }
        }
    }

    private void CanvasText_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CellViewModel cell })
            return;
        if (!cell.IsText && !cell.IsBoardElement)
            return;

        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (isShift)
        {
            // Shift+double-click: Open text editor
            FullImage.IsVisible = false;
            FullText.IsVisible = true;
            FullText.Text = cell.TextContent;
            _editingTextCell = cell;
            FullMediaOverlay.IsVisible = true;
        }
        else
        {
            // Normal double-click: Zoom to fill screen completely
            ClearSelection();
            cell.IsSelected = true;
            _selectedCells.Add(cell);
            UpdateSelectionState();
            ZoomToCell(cell);
        }
    }

    private void FullText_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_editingTextCell == null)
            return;
        _editingTextCell.TextContent = FullText.Text;
        Vm.MarkUnsaved();
    }

    private void CloseFullMedia_Click(object? sender, RoutedEventArgs e)
    {
        FullMediaOverlay.IsVisible = false;
        FullText.IsVisible = false;
        _editingTextCell = null;
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        FullMediaOverlay.IsVisible = false;
        FullText.IsVisible = false;
        _editingTextCell = null;
    }

    #endregion
}
