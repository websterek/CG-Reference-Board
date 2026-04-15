using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;
using CGReferenceBoard.Services;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a single cell on the reference board (image, text, video, label, or backdrop).
/// </summary>
public class CellViewModel : ViewModelBase, IDisposable
{
    #region Position

    private double _canvasX;
    /// <summary>Grid-snapped X position on the canvas.</summary>
    public double CanvasX
    {
        get => _canvasX;
        set
        {
            if (SetProperty(ref _canvasX, value))
                OnPropertyChanged(nameof(VisualX));
        }
    }

    private double _canvasY;
    /// <summary>Grid-snapped Y position on the canvas.</summary>
    public double CanvasY
    {
        get => _canvasY;
        set
        {
            if (SetProperty(ref _canvasY, value))
                OnPropertyChanged(nameof(VisualY));
        }
    }

    /// <summary>Effective visual X, offset by backdrop padding when applicable.</summary>
    public double VisualX => _canvasX - (Type == CellType.Backdrop ? Constants.BackdropPadding : 0);

    /// <summary>Effective visual Y, offset by backdrop padding when applicable.</summary>
    public double VisualY => _canvasY - (Type == CellType.Backdrop ? Constants.BackdropPadding : 0);

    #endregion

    #region Size

    private int _colSpan = 1;
    /// <summary>Number of grid columns this cell spans.</summary>
    public int ColSpan
    {
        get => _colSpan;
        set
        {
            if (SetProperty(ref _colSpan, value))
                OnPropertyChanged(nameof(PixelWidth));
        }
    }

    private int _rowSpan = 1;
    /// <summary>Number of grid rows this cell spans.</summary>
    public int RowSpan
    {
        get => _rowSpan;
        set
        {
            if (SetProperty(ref _rowSpan, value))
                OnPropertyChanged(nameof(PixelHeight));
        }
    }

    /// <summary>Total pixel width including backdrop visual padding when applicable.
    /// Backdrops are drawn to extend by <see cref="Constants.BackdropPadding"/> on both
    /// the left and right sides, so the extra space equals 2 × BackdropPadding.
    /// </summary>
    public double PixelWidth => ColSpan * Constants.GridSize + (Type == CellType.Backdrop ? 2 * Constants.BackdropPadding : 0);

    /// <summary>Total pixel height including backdrop visual padding when applicable.
    /// Backdrops are drawn to extend by <see cref="Constants.BackdropPadding"/> on both
    /// the top and bottom sides, so the extra space equals 2 × BackdropPadding.
    /// </summary>
    public double PixelHeight => RowSpan * Constants.GridSize + (Type == CellType.Backdrop ? 2 * Constants.BackdropPadding : 0);

    #endregion

    #region Type & State

    private CellType _type = CellType.None;
    /// <summary>The content type of this cell.</summary>
    public CellType Type
    {
        get => _type;
        set
        {
            if (!SetProperty(ref _type, value))
                return;

            // Position and size depend on type (backdrop has padding)
            OnPropertyChanged(nameof(VisualX));
            OnPropertyChanged(nameof(VisualY));
            OnPropertyChanged(nameof(PixelWidth));
            OnPropertyChanged(nameof(PixelHeight));

            // Derived boolean flags
            OnPropertyChanged(nameof(IsImage));
            OnPropertyChanged(nameof(IsVideo));
            OnPropertyChanged(nameof(IsText));
            OnPropertyChanged(nameof(IsLabel));
            OnPropertyChanged(nameof(IsBackdrop));
            OnPropertyChanged(nameof(IsFile));
            OnPropertyChanged(nameof(IsBoardElement));
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(HasTextContent));
            OnPropertyChanged(nameof(ShowIcon));
            OnPropertyChanged(nameof(TypeIcon));
            OnPropertyChanged(nameof(ZIndex));
            OnPropertyChanged(nameof(NeedsImage));
            OnPropertyChanged(nameof(ShowPlaceholder));
            OnPropertyChanged(nameof(ShowTextContent));
            OnPropertyChanged(nameof(ShowLabelContent));
            OnPropertyChanged(nameof(ShowIconBadge));

            // Context-menu visibility flags derived from Type
            OnPropertyChanged(nameof(HasAppearanceOptions));
            OnPropertyChanged(nameof(HasArrangeOptions));
            OnPropertyChanged(nameof(HasFileOptions));
            OnPropertyChanged(nameof(HasTextOptions));
            OnPropertyChanged(nameof(HasClipboardOptions));
        }
    }

    private bool _isDownloading;
    /// <summary>Whether this cell is currently downloading content (e.g. video from URL).</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    private float _downloadProgress;
    /// <summary>Download progress 0–100. Updated during active yt-dlp downloads.</summary>
    public float DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    private string _downloadStatusText = "Downloading...";
    /// <summary>Human-readable download status string, e.g. "Downloading 45% @ 1.5MiB/s ETA 00:30".</summary>
    public string DownloadStatusText
    {
        get => _downloadStatusText;
        set => SetProperty(ref _downloadStatusText, value);
    }

    private bool _isSelected;
    /// <summary>Whether this cell is currently selected (for multi-selection operations).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _hasMultipleSelection;
    public bool HasMultipleSelection
    {
        get => _hasMultipleSelection;
        set => SetProperty(ref _hasMultipleSelection, value);
    }

    private bool _hasSingleSelection;
    public bool HasSingleSelection
    {
        get => _hasSingleSelection;
        set => SetProperty(ref _hasSingleSelection, value);
    }

    private bool _isDragInvalid;
    /// <summary>Whether this cell is being dragged to an invalid position (collision detected).</summary>
    public bool IsDragInvalid
    {
        get => _isDragInvalid;
        set => SetProperty(ref _isDragInvalid, value);
    }

    private bool _isDragging;
    /// <summary>Whether this cell is currently being dragged.</summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (SetProperty(ref _isDragging, value))
                OnPropertyChanged(nameof(ZIndex));
        }
    }

    private bool _isHighlighted;
    /// <summary>Whether this cell is temporarily highlighted (e.g. after paste).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    private bool _isInViewport = true;
    /// <summary>
    /// Controlled by the viewport LOD system. When false the cell lies outside
    /// the padded viewport region; its entire visual tree is collapsed to save
    /// layout and render work. Defaults to true so cells appear immediately on load.
    /// </summary>
    public bool IsInViewport
    {
        get => _isInViewport;
        set => SetProperty(ref _isInViewport, value);
    }

    private bool _isDetailVisible = true;
    /// <summary>
    /// True when the cell is large enough on screen to justify rendering
    /// text bodies, icon badges, and other detail elements.
    /// False when zoomed far out. Updated by the viewport LOD system.
    /// </summary>
    public bool IsDetailVisible
    {
        get => _isDetailVisible;
        set
        {
            if (SetProperty(ref _isDetailVisible, value))
            {
                OnPropertyChanged(nameof(ShowTextContent));
                OnPropertyChanged(nameof(ShowLabelContent));
                OnPropertyChanged(nameof(ShowIconBadge));
            }
        }
    }

    public bool HasAppearanceOptions => IsBoardElement || IsFile || IsImage;
    public bool HasArrangeOptions => !IsBackdrop;
    public bool HasFileOptions => IsFile;
    public bool HasTextOptions => HasTextContent;
    public bool HasClipboardOptions => IsImage || IsFile || HasTextContent;

    private bool _isHitTestEnabled = true;
    public bool IsHitTestEnabled
    {
        get => _isHitTestEnabled;
        set => SetProperty(ref _isHitTestEnabled, value);
    }

    #endregion

    #region Derived Type Flags

    public bool IsImage => Type == CellType.Image;
    public bool IsVideo => Type == CellType.Video;
    public bool IsText => Type == CellType.Text;
    public bool IsLabel => Type == CellType.Label;
    public bool IsBackdrop => Type == CellType.Backdrop;

    /// <summary>True for types that reference a file on disk (Image or Video).</summary>
    public bool IsFile => Type == CellType.Image || Type == CellType.Video;

    /// <summary>True for board-level decorative elements (Label or Backdrop).</summary>
    public bool IsBoardElement => Type == CellType.Label || Type == CellType.Backdrop;

    /// <summary>True if the cell has any content assigned.</summary>
    public bool HasContent => Type != CellType.None;

    /// <summary>True for types that display editable text content.</summary>
    public bool HasTextContent => Type == CellType.Text || Type == CellType.Label || Type == CellType.Backdrop;

    /// <summary>Whether to show the type icon overlay (shown for content cells, hidden for board elements).</summary>
    public bool ShowIcon => HasContent && !IsBoardElement;

    /// <summary>True when the Text-cell content body should be rendered.</summary>
    public bool ShowTextContent => IsText && _isDetailVisible;

    /// <summary>True when the Label-cell content body should be rendered.</summary>
    public bool ShowLabelContent => IsLabel && _isDetailVisible;

    /// <summary>True when the type icon badge should be rendered.</summary>
    public bool ShowIconBadge => ShowIcon && _isDetailVisible;

    /// <summary>True when this cell type uses a bitmap (Image or Video).</summary>
    public bool NeedsImage => Type == CellType.Image || Type == CellType.Video;

    /// <summary>True when the placeholder color rect should be shown instead of an image.</summary>
    public bool ShowPlaceholder => NeedsImage && _image == null;

    /// <summary>
    /// Collision layer for overlap prevention. Cells only block each other within the same layer.
    /// Layer 0 = Backdrops, Layer 1 = Content (Image/Video/Text), Layer 2 = Labels.
    /// </summary>
    public int CollisionLayer => Type switch
    {
        CellType.Backdrop => 0,
        CellType.Label => 2,
        _ => 1   // Image, Video, Text
    };

    /// <summary>
    /// Z-index for rendering order with proper layering:
    /// Layering hierarchy (lowest to highest):
    /// - Backdrops (static): -10
    /// - Content/Items (static): 0
    /// - Labels (static): 10
    /// - Backdrops (dragging): 90
    /// - Content/Items (dragging): 120
    /// - Labels (dragging): 150
    /// - Annotations (always): 200 (set in XAML)
    ///
    /// This ensures:
    /// - Items are always above backdrops
    /// - Labels are always above items and backdrops
    /// - Dragging items maintain proper order relative to each other
    /// - Annotations are always on top
    /// </summary>
    public int ZIndex
    {
        get
        {
            // Apply type-specific boost when dragging to maintain proper layering
            if (IsDragging)
            {
                return Type switch
                {
                    CellType.Backdrop => 90,    // Above all static items, below dragging content
                    CellType.Label => 150,       // Above all dragging items, below annotations
                    _ => 120                     // Content items - above dragging backdrops
                };
            }

            return Type switch
            {
                CellType.Backdrop => -10,
                CellType.Label => 10,
                _ => 0
            };
        }
    }

    #endregion

    #region Type Icon (Material Design SVG paths)

    /// <summary>Material Design icon path data for the cell's type.</summary>
    public string TypeIcon => Type switch
    {
        CellType.Image => "M200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h560q33 0 56.5 23.5T840-760v560q0 33-23.5 56.5T760-120H200Zm0-80h560v-560H200v560Zm40-80h480L570-480 450-320l-90-120-120 160Zm-40 80v-560 560Z",
        CellType.Text => "M200-200h560v-367L567-760H200v560Zm0 80q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h400l240 240v400q0 33-23.5 56.5T760-120H200Zm80-160h400v-80H280v80Zm0-160h400v-80H280v80Zm0-160h280v-80H280v80Zm-80 400v-560 560Z",
        CellType.Video => "m160-800 80 160h120l-80-160h80l80 160h120l-80-160h80l80 160h120l-80-160h120q33 0 56.5 23.5T880-720v480q0 33-23.5 56.5T800-160H160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800Zm0 240v320h640v-320H160Zm0 0v320-320Z",
        CellType.Label => "M440-160q-17 0-32-6t-26-18L138-428q-11-11-17.5-26T114-485v-235q0-33 23.5-56.5T194-800h235q16 0 31.5 6.5T486-776l244 244q11 11 17.5 26t6.5 31q0 16-6.5 31.5T730-418L498-186q-11 11-26 17.5T440-160Z",
        CellType.Backdrop => "M200-120q-33 0-56.5-23.5T120-200v-560q0-33 23.5-56.5T200-840h560q33 0 56.5 23.5T840-760v560q0 33-23.5 56.5T760-120H200Zm0-80h560v-560H200v560Z",
        _ => ""
    };

    #endregion

    #region Appearance

    private string _backgroundColor = "#885A3A10";
    /// <summary>Background color for backdrop cells.</summary>
    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }

    private string _foregroundColor = "#FFFFA500";
    /// <summary>Foreground (text) color for labels and backdrops.</summary>
    public string ForegroundColor
    {
        get => _foregroundColor;
        set => SetProperty(ref _foregroundColor, value);
    }

    private string _imageStretch = "UniformToFill";
    /// <summary>Image stretch mode: "UniformToFill" or "Uniform".</summary>
    public string ImageStretch
    {
        get => _imageStretch;
        set => SetProperty(ref _imageStretch, value);
    }

    private double _fontSize = 48.0;
    /// <summary>Font size for label text.</summary>
    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    #endregion

    #region Content

    private string? _filePath;
    /// <summary>Path to the image file or video thumbnail on disk.</summary>
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    private string? _videoPath;
    /// <summary>Path to the video file on disk (only for Video type).</summary>
    public string? VideoPath
    {
        get => _videoPath;
        set => SetProperty(ref _videoPath, value);
    }

    private string? _textContent;
    /// <summary>Text content for Text, Label, and Backdrop cells.</summary>
    public string? TextContent
    {
        get => _textContent;
        set => SetProperty(ref _textContent, value);
    }

    private Bitmap? _image;
    /// <summary>Loaded bitmap for display (image content or video thumbnail).</summary>
    public Bitmap? Image
    {
        get => _image;
        set
        {
            if (SetProperty(ref _image, value))
                OnPropertyChanged(nameof(ShowPlaceholder));
        }
    }

    #endregion

    #region LOD (Level-of-Detail) Image Lifecycle

    private string _placeholderColor = "#FF2A2A2A";
    /// <summary>
    /// Average colour of the source image, shown as a placeholder rectangle
    /// when the full bitmap is not loaded (off-screen or zoomed far out).
    /// Persisted in the .cgrb file so we never need to decode just for the colour.
    /// </summary>
    public string PlaceholderColor
    {
        get => _placeholderColor;
        set => SetProperty(ref _placeholderColor, value);
    }

    private string? _thumbnailPath;
    /// <summary>
    /// Path to a small (~200 px wide) JPEG thumbnail generated on first load.
    /// Stored in a .thumbs subdirectory next to the source image.
    /// Not persisted — regenerated on demand.
    /// </summary>
    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set => SetProperty(ref _thumbnailPath, value);
    }

    private ImageLod _currentLod = ImageLod.Full;
    /// <summary>The LOD tier currently loaded for this cell's image.</summary>
    public ImageLod CurrentLod
    {
        get => _currentLod;
        internal set => SetProperty(ref _currentLod, value);
    }

    /// <summary>
    /// Serial token incremented on every LOD transition.
    /// Used to discard stale async loads that complete after a newer request.
    /// </summary>
    private int _lodToken;

    /// <summary>
    /// Transitions this cell to the requested LOD, loading or unloading the bitmap
    /// as needed. Safe to call repeatedly — no-ops if already at the target LOD.
    /// Must be called on the UI thread; the heavy I/O runs on the thread-pool.
    /// </summary>
    public async Task ApplyLodAsync(ImageLod target)
    {
        // Only image / video cells have bitmaps to manage.
        if (!NeedsImage)
            return;

        // Already at the target level — nothing to do.
        if (target == _currentLod && (_currentLod == ImageLod.Placeholder || _image != null))
            return;

        int token = Interlocked.Increment(ref _lodToken);

        if (target == ImageLod.Placeholder)
        {
            var old = _image;
            Image = null;
            CurrentLod = ImageLod.Placeholder;
            old?.Dispose();
            return;
        }

        // Capture paths for the background closure.
        var filePath = _filePath;
        var thumbPath = _thumbnailPath;

        // All I/O (File.Exists, thumbnail generation, bitmap decode) on thread-pool.
        Bitmap? newBitmap = null;
        try
        {
            newBitmap = await Task.Run(async () =>
            {
                string? pathToLoad = null;

                if (target == ImageLod.Thumbnail)
                {
                    if (thumbPath == null || !System.IO.File.Exists(thumbPath))
                    {
                        thumbPath = await ImageManager.EnsureThumbnailAsync(filePath);
                    }
                    pathToLoad = thumbPath ?? filePath;
                }
                else
                {
                    pathToLoad = filePath;
                }

                if (string.IsNullOrEmpty(pathToLoad) || !System.IO.File.Exists(pathToLoad))
                    return null;

                // Select decode width by tier: thumbnail file is already the right size,
                // Medium decodes at 512 px, Full decodes at 2048 px.
                int decodeWidth = target switch
                {
                    ImageLod.Thumbnail => int.MaxValue,
                    ImageLod.Medium => ImageManager.MaxMediumDecodeWidth,
                    _ => ImageManager.MaxFullDecodeWidth,
                };
                try
                { return ImageManager.LoadBitmapFromPath(pathToLoad, decodeWidth); }
                catch { return null; }
            });
        }
        catch
        {
            return;
        }

        if (token != _lodToken)
        {
            newBitmap?.Dispose();
            return;
        }

        // If decode failed, fall back to placeholder to avoid infinite retry.
        if (newBitmap == null)
        {
            CurrentLod = ImageLod.Placeholder;
            return;
        }

        // Update thumbnail path if it was generated during this load.
        // Use the property setter (not the backing field) so UI bindings are notified.
        if (target == ImageLod.Thumbnail && thumbPath != _thumbnailPath)
            ThumbnailPath = thumbPath;

        var oldBitmap = _image;
        Image = newBitmap;
        CurrentLod = target;
        oldBitmap?.Dispose();
    }

    /// <summary>
    /// Unloads the bitmap (sets Image to null) and drops to the Placeholder LOD.
    /// Equivalent to <c>ApplyLodAsync(ImageLod.Placeholder)</c> but synchronous.
    /// </summary>
    public void UnloadImage()
    {
        if (_image == null && _currentLod == ImageLod.Placeholder)
            return;

        Interlocked.Increment(ref _lodToken); // cancel any in-flight loads

        var old = _image;
        Image = null;
        CurrentLod = ImageLod.Placeholder;
        old?.Dispose();
    }

    /// <summary>
    /// Ensures <see cref="PlaceholderColor"/> is populated by computing the average
    /// colour from the source image. Runs the heavy work on the thread-pool.
    /// </summary>
    public async Task EnsurePlaceholderColorAsync()
    {
        if (!NeedsImage || string.IsNullOrEmpty(_filePath))
            return;

        // Skip if we already have a meaningful (non-default) colour.
        if (_placeholderColor != "#FF2A2A2A")
            return;

        var color = await ImageManager.ComputeAverageColorAsync(_filePath);
        PlaceholderColor = color;
    }

    #endregion

    #region Content Setters

    /// <summary>
    /// Configures this cell as an Image without loading any bitmap.
    /// Used during board deserialization so the viewport LOD system
    /// can decide the appropriate detail level later.
    /// </summary>
    public void SetImageDeferred(string path)
    {
        FilePath = path;
        Type = CellType.Image;
        CurrentLod = ImageLod.Placeholder;
        // Image stays null — the viewport timer will call ApplyLodAsync when visible.
        // Kick off average-colour computation in the background so the placeholder
        // rect shows the real colour even before the bitmap is first loaded.
        // EnsurePlaceholderColorAsync is a no-op when the colour is already known.
        _ = EnsurePlaceholderColorAsync();
    }

    /// <summary>
    /// Configures this cell as a Video without loading any bitmap.
    /// Used during board deserialization so the viewport LOD system
    /// can decide the appropriate detail level later.
    /// </summary>
    public void SetVideoDeferred(string videoPath, string thumbPath)
    {
        FilePath = thumbPath;
        VideoPath = videoPath;
        Type = CellType.Video;
        CurrentLod = ImageLod.Placeholder;
        // Image stays null — the viewport timer will call ApplyLodAsync when visible.
        _ = EnsurePlaceholderColorAsync();
    }

    /// <summary>
    /// Loads an image from disk and sets this cell to Image type.
    /// The image is loaded at full LOD immediately (for newly-placed cells that are on-screen).
    /// </summary>
    public void SetImage(string path)
    {
        // Load the bitmap BEFORE touching any cell state so that a decode failure
        // leaves the cell completely unchanged — no partial property updates, no leaks.
        Bitmap? newBitmap;
        try
        {
            newBitmap = ImageManager.LoadBitmapFromPath(path, ImageManager.MaxFullDecodeWidth)
                        ?? new Bitmap(path);
        }
        catch
        {
            // Image file is missing or corrupt — leave cell state unchanged.
            return;
        }

        var old = _image;
        Image = newBitmap;
        old?.Dispose();
        FilePath = path;
        Type = CellType.Image;
        CurrentLod = ImageLod.Full;

        // Kick off background work: thumbnail + average colour.
        var bgToken = Interlocked.Increment(ref _lodToken);
        _ = Task.Run(async () =>
        {
            var thumb = await ImageManager.EnsureThumbnailAsync(path);
            var color = await ImageManager.ComputeAverageColorAsync(path);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (bgToken != _lodToken)
                    return; // cell was cleared/reassigned
                ThumbnailPath = thumb;
                PlaceholderColor = color;
            });
        });
    }

    /// <summary>
    /// Sets this cell to Video type with the given video and thumbnail paths.
    /// The thumbnail image is loaded if possible; if not, the cell displays a placeholder.
    /// </summary>
    public void SetVideo(string videoPath, string thumbPath)
    {
        VideoPath = videoPath;
        FilePath = thumbPath;
        Type = CellType.Video;

        // Try to load the thumbnail as a bitmap.
        // thumbPath may be the video file itself (local import without a separate thumbnail),
        // in which case the bitmap load will fail gracefully → placeholder is shown.
        Bitmap? newBitmap = null;
        try
        {
            newBitmap = ImageManager.LoadBitmapFromPath(thumbPath, ImageManager.MaxFullDecodeWidth);
        }
        catch { /* not a valid image — will use placeholder */ }

        var old = _image;
        if (newBitmap != null)
        {
            Image = newBitmap;
            CurrentLod = ImageLod.Full;
        }
        else
        {
            Image = null;
            CurrentLod = ImageLod.Placeholder;
        }
        old?.Dispose();

        // Kick off background work: thumbnail + average colour.
        var bgToken = Interlocked.Increment(ref _lodToken);
        _ = Task.Run(async () =>
        {
            string? effectiveThumb = thumbPath;
            // If the "thumbnail" is actually a video file, we can't generate a thumb from it here.
            // The ImageManager will return null, and we'll just use the placeholder color.
            var thumb = await ImageManager.EnsureThumbnailAsync(effectiveThumb);
            var color = await ImageManager.ComputeAverageColorAsync(effectiveThumb);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (bgToken != _lodToken)
                    return;
                ThumbnailPath = thumb;
                if (color != "#FF2A2A2A")
                    PlaceholderColor = color;
            });
        });
    }

    /// <summary>
    /// Sets the text content. For non-board-element cells, changes type to Text.
    /// </summary>
    public void SetText(string text)
    {
        // Cancel any in-flight LOD load so it cannot overwrite the text cell
        // with a bitmap after we've already switched to CellType.Text.
        Interlocked.Increment(ref _lodToken);

        TextContent = text;
        if (!IsBoardElement)
            Type = CellType.Text;
        var old = _image;
        Image = null;
        CurrentLod = ImageLod.Placeholder;
        old?.Dispose();
        FilePath = null;
    }

    /// <summary>
    /// Resets the cell to an empty state, clearing all content and properties.
    /// Also disposes the bitmap. Use this when removing a cell from the board
    /// or when resetting a cell to its default/empty state.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref _lodToken);
        var old = _image;
        Type = CellType.None;
        Image = null;
        TextContent = null;
        FilePath = null;
        VideoPath = null;
        ThumbnailPath = null;
        PlaceholderColor = "#FF2A2A2A";
        CurrentLod = ImageLod.Placeholder;
        old?.Dispose();
    }

    /// <summary>
    /// Releases only the held bitmap without clearing other properties.
    /// Use this when you want to unload the image to free memory but keep
    /// the cell's position, type, and other properties intact (e.g., when
    /// switching boards or before discarding the view-model without calling Clear).
    /// </summary>
    public void Dispose()
    {
        // Cancel any in-flight LOD load so it cannot resurrect the bitmap.
        Interlocked.Increment(ref _lodToken);
        var old = _image;
        _image = null;
        old?.Dispose();
    }

    #endregion
}
