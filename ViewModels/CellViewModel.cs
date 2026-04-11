using Avalonia.Media.Imaging;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.Models;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a single cell on the reference board (image, text, video, label, or backdrop).
/// </summary>
public class CellViewModel : ViewModelBase
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

    /// <summary>Total pixel width including backdrop padding when applicable.</summary>
    public double PixelWidth => ColSpan * Constants.GridSize + (Type == CellType.Backdrop ? Constants.GridSize : 0);

    /// <summary>Total pixel height including backdrop padding when applicable.</summary>
    public double PixelHeight => RowSpan * Constants.GridSize + (Type == CellType.Backdrop ? Constants.GridSize : 0);

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
        }
    }

    private bool _isDownloading;
    /// <summary>Whether this cell is currently downloading content (e.g. video from URL).</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    private bool _isSelected;
    /// <summary>Whether this cell is currently selected (for multi-selection operations).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
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

    /// <summary>Z-index for rendering order: backdrops behind, labels in front.</summary>
    public int ZIndex => Type switch
    {
        CellType.Backdrop => -10,
        CellType.Label => 5,
        _ => 0
    };

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
        set => SetProperty(ref _image, value);
    }

    #endregion

    #region Content Setters

    /// <summary>
    /// Loads an image from disk and sets this cell to Image type.
    /// </summary>
    public void SetImage(string path)
    {
        try
        {
            Image = new Bitmap(path);
            FilePath = path;
            Type = CellType.Image;
        }
        catch
        {
            // Image file may be missing or corrupt
        }
    }

    /// <summary>
    /// Sets this cell to Video type with the given video and thumbnail paths.
    /// </summary>
    public void SetVideo(string videoPath, string thumbPath)
    {
        try
        {
            Image = new Bitmap(thumbPath);
            FilePath = thumbPath;
            VideoPath = videoPath;
            Type = CellType.Video;
        }
        catch
        {
            // Thumbnail file may be missing or corrupt
        }
    }

    /// <summary>
    /// Sets the text content. For non-board-element cells, changes type to Text.
    /// </summary>
    public void SetText(string text)
    {
        TextContent = text;
        if (!IsBoardElement)
            Type = CellType.Text;
        Image = null;
        FilePath = null;
    }

    /// <summary>
    /// Resets the cell to an empty state.
    /// </summary>
    public void Clear()
    {
        Type = CellType.None;
        Image = null;
        TextContent = null;
        FilePath = null;
        VideoPath = null;
    }

    #endregion
}
