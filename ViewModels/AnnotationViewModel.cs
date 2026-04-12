using Avalonia;
using System.Collections.ObjectModel;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a single annotation (drawing, shape, or text note) on the board's annotation layer.
/// </summary>
public class AnnotationViewModel : ViewModelBase
{
    private double _canvasX;
    /// <summary>Canvas X offset for the entire annotation group.</summary>
    public double CanvasX
    {
        get => _canvasX;
        set => SetProperty(ref _canvasX, value);
    }

    private double _canvasY;
    /// <summary>Canvas Y offset for the entire annotation group.</summary>
    public double CanvasY
    {
        get => _canvasY;
        set => SetProperty(ref _canvasY, value);
    }

    private string _color = "#FFFF4444";
    /// <summary>Stroke/fill color in ARGB hex format.</summary>
    public string Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    private double _thickness = 4.0;
    /// <summary>Stroke thickness in pixels.</summary>
    public double Thickness
    {
        get => _thickness;
        set => SetProperty(ref _thickness, value);
    }

    /// <summary>Ordered collection of points that define the annotation shape.</summary>
    public ObservableCollection<Point> Points { get; set; } = new();

    private string _type = "Brush";
    /// <summary>Annotation tool type: Brush, Rectangle, Ellipse, Arrow, or Text.</summary>
    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    private string _text = "";
    /// <summary>Text content (only used when <see cref="Type"/> is "Text").</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    private bool _isSelected;
    /// <summary>Whether this annotation is currently selected for moving/editing.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                OnPropertyChanged(nameof(IsHitTestable));
        }
    }

    private bool _isInDrawMode;
    /// <summary>Whether the application is currently in draw/annotation mode.</summary>
    public bool IsInDrawMode
    {
        get => _isInDrawMode;
        set
        {
            if (SetProperty(ref _isInDrawMode, value))
                OnPropertyChanged(nameof(IsHitTestable));
        }
    }

    /// <summary>
    /// Whether this annotation should receive pointer events.
    /// True when in draw mode OR when the annotation is selected.
    /// </summary>
    public bool IsHitTestable => IsInDrawMode || IsSelected;
}
