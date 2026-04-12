using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Views;

public partial class MainWindow
{
    #region Window Chrome Handlers

    private void TopBar_DoubleTapped(object? sender, TappedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            this.BeginMoveDrag(e);
    }

    private void MinimizeWindow_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object? sender, RoutedEventArgs e) => Close();

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

    private void Undo_Click(object? sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object? sender, RoutedEventArgs e) => Redo();
    private void GridMode_Click(object? sender, RoutedEventArgs e) => IsDrawMode = false;
    private void AnnotationMode_Click(object? sender, RoutedEventArgs e) => IsDrawMode = true;
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

        _currentBoardFile = file.Path.LocalPath;
        _workspaceDir = Path.GetDirectoryName(_currentBoardFile)!;
        CurrentBoardName = Path.GetFileNameWithoutExtension(_currentBoardFile);
        OnPropertyChanged(nameof(WindowTitle));
        UpdateBoardDirectoryList();

        Directory.CreateDirectory(Path.Combine(_workspaceDir, "images"));
        Directory.CreateDirectory(Path.Combine(_workspaceDir, "videos"));

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;

        OnPropertyChanged(nameof(WindowTitle));
        SaveBoardData();
        ShowToast("💾 Saved");
    }

    private async void LoadBoard_Click(object? sender, RoutedEventArgs e)
    {
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
            LoadBoardFromFile(files[0].Path.LocalPath);
            ShowToast("📂 Opened");
        }
    }

    private void NewBoard_Click(object? sender, RoutedEventArgs e)
    {
        _currentBoardFile = "";
        GridCells.Clear();
        Annotations.Clear();
        CurrentBoardName = "New Board";
        _hasUnsavedChanges = false;
        Title = Constants.AppName;
        UpdateBoardDirectoryList();

        var startupOverlay = this.FindControl<Border>("StartupOverlay");
        if (startupOverlay != null)
            startupOverlay.IsVisible = false;

        OnPropertyChanged(nameof(WindowTitle));
        ShowToast("📄 New Board");
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

            while (GridCells.Any(c => (int)c.CanvasX == x && (int)c.CanvasY == y))
            {
                x += (int)Constants.GridSize;
                if (x > 1600)
                { x = startX; y += (int)Constants.GridSize; }
            }

            var cell = new CellViewModel { CanvasX = x, CanvasY = y };
            GridCells.Add(cell);
            HighlightCell(cell);

            string ext = Path.GetExtension(file.Path.LocalPath).ToLowerInvariant();
            if (videoExtensions.Contains(ext))
            {
                string destDir = Path.Combine(_workspaceDir, "videos");
                Directory.CreateDirectory(destDir);
                string destPath = Path.Combine(destDir, Path.GetFileName(file.Path.LocalPath));
                if (file.Path.LocalPath != destPath && !File.Exists(destPath))
                    File.Copy(file.Path.LocalPath, destPath);

                // Try to extract a thumbnail frame via ffmpeg
                string thumbDir = Path.Combine(_workspaceDir, "images");
                string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                cell.SetVideo(destPath, thumbPath ?? destPath);
                MarkUnsaved();
                SaveBoardData();
            }
            else
            {
                LoadImageToCell(cell, file.Path.LocalPath);
            }
        }
        ShowToast("📥 Imported");
    }

    private void BoardDir_Click(object? sender, RoutedEventArgs e)
        => PlatformHelper.OpenDirectory(_workspaceDir);

    private void BoardMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is MenuItem item && item.DataContext is BoardMenuItemViewModel vm)
        {
            var path = Path.Combine(_workspaceDir, vm.FileName);
            if (File.Exists(path))
            {
                _currentBoardFile = path;
                CurrentBoardName = Path.GetFileNameWithoutExtension(path);
                LoadBoardFromFile(path);
                ShowToast("📂 Opened");
            }
        }
    }

    private void RecentBoard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is string path)
        {
            LoadBoardFromFile(path);
            ShowToast("📂 Opened");
        }
    }

    #endregion

    #region Annotation Tool Mode Handlers

    private void BrushMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Brush"; }

    private void TextMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Text"; }

    private void ArrowMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Arrow"; }

    private void SquareMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Rectangle"; }

    private void CircleMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Ellipse"; }

    private void EraserMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Eraser"; }

    private void MoveMode_Click(object? sender, RoutedEventArgs e)
    { CurrentTool = "Move"; }

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
            ShowToast("📋 Copied");
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
            ShowToast("📋 Copied");
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
            ShowToast("📋 Copied");
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

        MarkUnsaved();
        SaveBoardData();
    }

    private void ToggleImageFit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsImage: true } cell })
        {
            cell.ImageStretch = cell.ImageStretch == "UniformToFill" ? "Uniform" : "UniformToFill";
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void IncreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell })
        {
            cell.FontSize += 8;
            MarkUnsaved();
            SaveBoardData();
        }
    }

    private void DecreaseFontSize_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: CellViewModel { IsLabel: true } cell } && cell.FontSize > 16)
        {
            cell.FontSize -= 8;
            MarkUnsaved();
            SaveBoardData();
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

        if (!GridLayoutService.IsSpaceEmpty(GridCells, cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, cell.CollisionLayer, excludeCell: cell))
        {
            ShakeScreen();
            return;
        }

        cell.ColSpan = newColSpan;
        cell.RowSpan = newRowSpan;

        MarkUnsaved();
        SaveBoardData();
    }

    private void DeleteCell_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;

        bool anyDeleted = false;

        // Delete all selected cells
        if (_selectedCells.Count > 0)
        {
            foreach (var cell in _selectedCells.ToList())
            {
                cell.Clear();
                GridCells.Remove(cell);
            }
            _selectedCells.Clear();
            _hoveredCell = null;
            anyDeleted = true;
        }

        // Delete all selected annotations
        if (_selectedAnnotations.Count > 0)
        {
            foreach (var ann in _selectedAnnotations.ToList())
                Annotations.Remove(ann);
            _selectedAnnotations.Clear();
            anyDeleted = true;
        }

        // Fallback: if nothing was selected, delete the right-clicked cell
        if (!anyDeleted && sender is MenuItem { DataContext: CellViewModel clickedCell })
        {
            clickedCell.Clear();
            GridCells.Remove(clickedCell);
            anyDeleted = true;
        }

        if (anyDeleted)
        {
            UpdateSelectionState();
            MarkUnsaved();
            SaveBoardData();
            ShowToast("🗑 Deleted");
        }
    }

    #endregion

    #region Add Content (Context Menu)

    private void AddText_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        var newCell = new CellViewModel { CanvasX = x, CanvasY = y, ColSpan = 2, RowSpan = 2 };
        newCell.Type = CellType.Text;
        newCell.SetText("New Text Block");

        GridCells.Add(newCell);
        HighlightCell(newCell);
        MarkUnsaved();
        SaveBoardData();
    }

    private void AddLabel_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
            return;
        var hoverHighlight = this.FindControl<Border>("HoverHighlight");
        if (hoverHighlight == null)
            return;

        double x = Canvas.GetLeft(hoverHighlight);
        double y = Canvas.GetTop(hoverHighlight);

        int colorIdx = Random.Shared.Next(Constants.BackdropBackgroundColors.Length);

        var newCell = new CellViewModel
        {
            CanvasX = x,
            CanvasY = y,
            ColSpan = 4,
            RowSpan = 2,
            BackgroundColor = Constants.BackdropBackgroundColors[colorIdx],
            ForegroundColor = Constants.BackdropForegroundColors[colorIdx]
        };
        newCell.Type = CellType.Label;
        newCell.SetText("New Label");

        GridCells.Add(newCell);
        HighlightCell(newCell);
        MarkUnsaved();
        SaveBoardData();
    }

    private void AddBackdrop_Click(object? sender, RoutedEventArgs e)
    {
        if (_isViewMode)
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

            double width = maxX - gridX + Constants.BackdropPadding;
            double height = maxY - gridY + Constants.BackdropPadding;

            int colSpan = (int)Math.Ceiling(width / Constants.GridSize);
            int rowSpan = (int)Math.Ceiling(height / Constants.GridSize);

            // Check for collision and find empty space if needed
            Point? finalPosition = null;
            if (GridLayoutService.IsSpaceEmpty(GridCells, gridX, gridY, colSpan, rowSpan, collisionLayer: 0))
            {
                finalPosition = new Point(gridX, gridY);
            }
            else
            {
                // Try to find nearby empty space
                finalPosition = GridLayoutService.FindEmptySpace(GridCells, gridX, gridY, colSpan, rowSpan, collisionLayer: 0);
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

            GridCells.Add(backdrop);
            HighlightCell(backdrop);
            MarkUnsaved();
            SaveBoardData();

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
            ShowPlacementPreview(gridX, gridY, _pendingBackdrop.ColSpan, _pendingBackdrop.RowSpan, collisionLayer: 0);
        }
    }

    private void SelectContent_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: CellViewModel cell } || !cell.IsBackdrop)
            return;

        ClearSelection();

        double left = cell.CanvasX;
        double top = cell.CanvasY;
        double right = left + cell.ColSpan * Constants.GridSize;
        double bottom = top + cell.RowSpan * Constants.GridSize;

        foreach (var c in GridCells)
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

        foreach (var ann in Annotations)
        {
            bool inRect = ann.Points.Any(p =>
            {
                double px = p.X + ann.CanvasX;
                double py = p.Y + ann.CanvasY;
                return px >= left && px <= right && py >= top && py <= bottom;
            });

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
        var cellsToAvoid = GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, currentX, currentY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer);

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

        GridLayoutService.MoveAnnotationsWithCells(Annotations, oldPositions);
        MarkUnsaved();
        SaveBoardData();
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
        var cellsToAvoid = GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, currentX, minY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
                cellsToAvoid.Add(cell);
            }

            currentX += cell.PixelWidth;
        }

        GridLayoutService.MoveAnnotationsWithCells(Annotations, oldPositions);
        MarkUnsaved();
        SaveBoardData();
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
        var cellsToAvoid = GridCells.Except(sortedCells).ToList();

        foreach (var cell in sortedCells)
        {
            var emptySpace = GridLayoutService.FindEmptySpace(cellsToAvoid, minX, currentY, cell.ColSpan, cell.RowSpan, cell.CollisionLayer);

            if (emptySpace != null)
            {
                cell.CanvasX = emptySpace.Value.X;
                cell.CanvasY = emptySpace.Value.Y;
                cellsToAvoid.Add(cell);
            }

            currentY += cell.PixelHeight;
        }

        GridLayoutService.MoveAnnotationsWithCells(Annotations, oldPositions);
        MarkUnsaved();
        SaveBoardData();
    }

    #endregion

    #region Drag & Drop

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy | DragDropEffects.Move;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_isViewMode)
        { e.Handled = true; return; }
        e.Handled = true;

        var dropPt = e.GetPosition(CanvasGrid);
        int gridX = (int)(Math.Floor(dropPt.X / Constants.GridSize) * Constants.GridSize);
        int gridY = (int)(Math.Floor(dropPt.Y / Constants.GridSize) * Constants.GridSize);

        var files = e.DataTransfer.TryGetFiles();
        if (files != null && files.Any())
        {
            string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };
            string[] videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
            string[] textExtensions = { ".txt", ".md", ".log", ".csv", ".json", ".xml" };

            double nextX = gridX;
            double nextY = gridY;
            int placedCount = 0;

            foreach (var file in files)
            {
                string path = file.Path.LocalPath;
                if (!File.Exists(path))
                    continue;

                string ext = Path.GetExtension(path).ToLowerInvariant();
                bool isImage = imageExtensions.Contains(ext);
                bool isVideo = videoExtensions.Contains(ext);
                bool isText = textExtensions.Contains(ext);

                if (!isImage && !isVideo && !isText)
                    continue; // Skip unsupported file types

                int colSpan = 2, rowSpan = 2;
                if (isImage)
                {
                    var dimensions = GridLayoutService.GetImageDimensions(path);
                    if (dimensions.HasValue)
                        (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                }

                var emptySpace = GridLayoutService.FindEmptySpace(
                    GridCells, nextX, nextY, colSpan, rowSpan, collisionLayer: 1);

                if (emptySpace == null)
                    continue;

                var cell = new CellViewModel
                {
                    CanvasX = emptySpace.Value.X,
                    CanvasY = emptySpace.Value.Y,
                    ColSpan = colSpan,
                    RowSpan = rowSpan
                };

                if (isVideo)
                {
                    string destDir = Path.Combine(_workspaceDir, "videos");
                    Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, Path.GetFileName(path));
                    if (path != destPath && !File.Exists(destPath))
                        File.Copy(path, destPath);

                    // Try to extract a thumbnail frame via ffmpeg
                    string thumbDir = Path.Combine(_workspaceDir, "images");
                    string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                    cell.SetVideo(destPath, thumbPath ?? destPath);
                }
                else if (isText)
                {
                    try
                    { cell.SetText(File.ReadAllText(path)); }
                    catch { continue; }
                }
                else
                {
                    string destDir = Path.Combine(_workspaceDir, "images");
                    Directory.CreateDirectory(destDir);
                    string destPath = Path.Combine(destDir, Path.GetFileName(path));
                    if (path != destPath && !File.Exists(destPath))
                        File.Copy(path, destPath);
                    cell.SetImage(destPath);
                }

                GridCells.Add(cell);
                HighlightCell(cell);
                placedCount++;

                nextX = emptySpace.Value.X + colSpan * Constants.GridSize;
            }

            if (placedCount > 0)
            {
                MarkUnsaved();
                SaveBoardData();
                ShowToast($"📥 Dropped {placedCount} item(s)");
            }
            return;
        }

        _draggingCell = null;
        _isPointerDown = false;
        _lastPressedEventArgs = null;
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

        if (e.Key == Key.N && isCtrl)
        { NewBoard_Click(null, null!); return; }

        if (e.Key == Key.O && isCtrl)
        { LoadBoard_Click(null, null!); return; }

        if (e.Key == Key.S && isCtrl)
        {
            if (!string.IsNullOrEmpty(_currentBoardFile))
            {
                SaveBoardData();
                ShowToast("💾 Saved");
            }
            else
                SaveBoard_Click(null, null!);
            return;
        }

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focused is TextBox { IsVisible: true })
            return;

        if (e.Key == Key.Z && isCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        { Redo(); return; }
        if (e.Key == Key.Y && isCtrl)
        { Redo(); return; }

        if (e.Key == Key.Z && isCtrl)
        { Undo(); return; }

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
                    ShowToast("📋 Image copied");
                }
                catch { }
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
                ShowToast("📋 Path copied");
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
                ShowToast("📋 Text copied");
                return;
            }
            return;
        }

        if (e.Key == Key.V && isCtrl)
        {
            if (_isViewMode)
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
                var cell = GetOrCreateContentCellAt(new Point(preferredX, preferredY));
                if (cell.HasContent && !cell.IsBoardElement)
                { ShakeScreen(); return; }

                if (text.Contains("youtube.com") || text.Contains("youtu.be") || text.StartsWith("http"))
                    await DownloadVideoToCell(cell, text);
                else
                    cell.SetText(text);
                HighlightCell(cell);
                SaveBoardData();
                ShowToast("📋 Pasted");
                return;
            }

            var pastedFiles = await data.TryGetFilesAsync();
            if (pastedFiles != null && pastedFiles.Any())
            {
                string filePath = pastedFiles.First().Path.LocalPath;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" };
                string[] videoExtensions = { ".mp4", ".webm", ".avi", ".mov", ".mkv" };
                string[] textExtensions = { ".txt", ".md", ".log", ".csv", ".json", ".xml" };

                bool isImage = imageExtensions.Contains(ext);
                bool isVideo = videoExtensions.Contains(ext);
                bool isText = textExtensions.Contains(ext);

                if (!isImage && !isVideo && !isText)
                {
                    ShowToast("⚠️ Unsupported file format");
                    return;
                }

                try
                {
                    int colSpan = 2, rowSpan = 2;
                    if (isImage)
                    {
                        var dimensions = GridLayoutService.GetImageDimensions(filePath);
                        if (dimensions != null)
                            (colSpan, rowSpan) = GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height);
                    }

                    Point? emptySpace = GridLayoutService.FindEmptySpace(GridCells, preferredX, preferredY, colSpan, rowSpan, collisionLayer: 1);

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

                    if (isVideo)
                    {
                        string destDir = Path.Combine(_workspaceDir, "videos");
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                        if (filePath != destPath && !File.Exists(destPath))
                            File.Copy(filePath, destPath);

                        // Try to extract a thumbnail frame via ffmpeg
                        string thumbDir = Path.Combine(_workspaceDir, "images");
                        string? thumbPath = await YtDlpService.ExtractThumbnailAsync(destPath, thumbDir);
                        newCell.SetVideo(destPath, thumbPath ?? destPath);
                    }
                    else if (isText)
                    {
                        newCell.SetText(File.ReadAllText(filePath));
                    }
                    else
                    {
                        string destDir = Path.Combine(_workspaceDir, "images");
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);
                        string destPath = Path.Combine(destDir, Path.GetFileName(filePath));
                        if (filePath != destPath && !File.Exists(destPath))
                            File.Copy(filePath, destPath);
                        newCell.SetImage(destPath);
                    }

                    // Verify the cell has content (SetImage/SetVideo may fail for corrupt files)
                    if (!newCell.HasContent)
                    {
                        ShowToast("⚠️ Could not load file");
                        return;
                    }

                    GridCells.Add(newCell);
                    HighlightCell(newCell);
                    MarkUnsaved();
                    SaveBoardData();
                    ShowToast("📋 Pasted");
                }
                catch
                {
                    ShowToast("⚠️ Could not load file");
                }
                return;
            }

            var bitmap = await data.TryGetBitmapAsync();
            if (bitmap != null)
            {
                string destDir = Path.Combine(_workspaceDir, "images");
                Directory.CreateDirectory(destDir);
                string path = Path.Combine(destDir, Guid.NewGuid() + ".png");
                bitmap.Save(path);

                var dimensions = GridLayoutService.GetImageDimensions(path);
                var (colSpan, rowSpan) = dimensions != null
                    ? GridLayoutService.CalculateOptimalCellSize(dimensions.Value.Width, dimensions.Value.Height)
                    : (2, 2);

                Point? emptySpace = GridLayoutService.FindEmptySpace(GridCells, preferredX, preferredY, colSpan, rowSpan, collisionLayer: 1);

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
                GridCells.Add(newCell);
                HighlightCell(newCell);
                MarkUnsaved();
                SaveBoardData();
                ShowToast("📋 Pasted");
                return;
            }

            SaveBoardData();
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
        { IsDrawMode = false; return; }

        if (e.Key == Key.D2 && isCtrl)
        { IsDrawMode = true; return; }

        if (e.Key == Key.A && isShift && !isCtrl)
        { IsAnnotationsVisible = !IsAnnotationsVisible; return; }

        // Annotation tool shortcuts (Photoshop-style)
        if (IsDrawMode && noModifiers)
        {
            switch (e.Key)
            {
                case Key.B:
                    CurrentTool = "Brush";
                    ShowToast("🖌️ Brush");
                    return;
                case Key.E:
                    CurrentTool = "Eraser";
                    ShowToast("🧹 Eraser");
                    return;
                case Key.T:
                    CurrentTool = "Text";
                    ShowToast("🔤 Text");
                    return;
                case Key.L:
                    CurrentTool = "Arrow";
                    ShowToast("➡️ Arrow");
                    return;
                case Key.U:
                    CurrentTool = "Rectangle";
                    ShowToast("▪️ Rectangle");
                    return;
                case Key.O:
                    CurrentTool = "Ellipse";
                    ShowToast("⚪ Ellipse");
                    return;
                case Key.V:
                    CurrentTool = "Move";
                    ShowToast("✥ Select/Move");
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

                    if (GridLayoutService.IsSpaceEmpty(GridCells, cell.CanvasX, cell.CanvasY, newColSpan, newRowSpan, cell.CollisionLayer, excludeCell: cell))
                    {
                        cell.ColSpan = newColSpan;
                        cell.RowSpan = newRowSpan;
                    }
                }

                MarkUnsaved();
                SaveBoardData();
            }
            return;
        }

        if (e.Key == Key.T && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.T && isCtrl)
        {
            if (_isViewMode)
                return;
            var cell = _hoveredCell ?? GetHighlightedCell();
            cell.SetText("New Description...");
            SaveBoardData();
            return;
        }

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (_isViewMode)
                return;

            bool anyDeleted = false;

            if (_selectedCells.Count > 0)
            {
                foreach (var cell in _selectedCells.ToList())
                {
                    cell.Clear();
                    GridCells.Remove(cell);
                }
                _selectedCells.Clear();
                _hoveredCell = null;
                anyDeleted = true;
            }

            if (_selectedAnnotations.Count > 0)
            {
                foreach (var ann in _selectedAnnotations.ToList())
                    Annotations.Remove(ann);
                _selectedAnnotations.Clear();
                anyDeleted = true;
            }

            if (anyDeleted)
            {
                MarkUnsaved();
                SaveBoardData();
            }
            else if (_hoveredCell != null)
            {
                _hoveredCell.Clear();
                GridCells.Remove(_hoveredCell);
                _hoveredCell = null;
                MarkUnsaved();
                SaveBoardData();
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
        MarkUnsaved();
        SaveBoardData();
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
