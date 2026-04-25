using Avalonia;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a single annotation (drawing, shape, or text note) on the board's annotation layer.
/// </summary>
public class AnnotationViewModel : ViewModelBase, CGReferenceBoard.Models.Transforms.ITransformable
{
    // ───────── Bounding-box cache (local coordinates, updated when points change) ─────────

    private double _bboxMinX = double.MaxValue;
    private double _bboxMinY = double.MaxValue;
    private double _bboxMaxX = double.MinValue;
    private double _bboxMaxY = double.MinValue;

    /// <summary>
    /// Rebuilds the local bounding box from all current points.
    /// Call whenever the Points collection changes.
    /// </summary>
    public void UpdateBoundsCache()
    {
        if (Points.Count == 0)
        {
            _bboxMinX = _bboxMinY = double.MaxValue;
            _bboxMaxX = _bboxMaxY = double.MinValue;
            NotifyBoundsChanged();
            return;
        }

        _bboxMinX = _bboxMinY = double.MaxValue;
        _bboxMaxX = _bboxMaxY = double.MinValue;

        foreach (var p in Points)
        {
            if (p.X < _bboxMinX)
                _bboxMinX = p.X;
            if (p.X > _bboxMaxX)
                _bboxMaxX = p.X;
            if (p.Y < _bboxMinY)
                _bboxMinY = p.Y;
            if (p.Y > _bboxMaxY)
                _bboxMaxY = p.Y;
        }
        NotifyBoundsChanged();
    }

    /// <summary>Raises PropertyChanged for all four AbsBounds* computed properties.</summary>
    private void NotifyBoundsChanged()
    {
        OnPropertyChanged(nameof(AbsBoundsLeft));
        OnPropertyChanged(nameof(AbsBoundsTop));
        OnPropertyChanged(nameof(AbsBoundsRight));
        OnPropertyChanged(nameof(AbsBoundsBottom));
    }

    /// <summary>Absolute canvas left edge (CanvasX + local min X).</summary>
    public double AbsBoundsLeft => CanvasX + (_bboxMinX == double.MaxValue ? 0 : _bboxMinX);
    /// <summary>Absolute canvas top edge (CanvasY + local min Y).</summary>
    public double AbsBoundsTop => CanvasY + (_bboxMinY == double.MaxValue ? 0 : _bboxMinY);
    /// <summary>Absolute canvas right edge (CanvasX + local max X).</summary>
    public double AbsBoundsRight => CanvasX + (_bboxMaxX == double.MinValue ? 0 : _bboxMaxX);
    /// <summary>Absolute canvas bottom edge (CanvasY + local max Y).</summary>
    public double AbsBoundsBottom => CanvasY + (_bboxMaxY == double.MinValue ? 0 : _bboxMaxY);

    // ───────── Viewport visibility ─────────

    private bool _isInViewport = true;
    /// <summary>
    /// Whether this annotation overlaps the current viewport.
    /// Set by the LOD timer; drives IsVisible on the AnnotationShape so
    /// off-screen annotations are excluded from layout and rendering.
    /// Defaults to true so annotations are visible before the first LOD pass.
    /// </summary>
    public bool IsInViewport
    {
        get => _isInViewport;
        set => SetProperty(ref _isInViewport, value);
    }

    private bool _isHitTestEnabled = true;
    public bool IsHitTestEnabled
    {
        get => _isHitTestEnabled;
        set => SetProperty(ref _isHitTestEnabled, value);
    }

    private double _canvasX;
    /// <summary>Canvas X offset for the entire annotation group.</summary>
    public double CanvasX
    {
        get => _canvasX;
        set
        {
            if (SetProperty(ref _canvasX, value))
                NotifyBoundsChanged();
        }
    }

    private double _canvasY;
    /// <summary>Canvas Y offset for the entire annotation group.</summary>
    public double CanvasY
    {
        get => _canvasY;
        set
        {
            if (SetProperty(ref _canvasY, value))
                NotifyBoundsChanged();
        }
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

    #region ITransformable

    /// <inheritdoc/>
    public double BoundsLeft => AbsBoundsLeft;

    /// <inheritdoc/>
    public double BoundsTop => AbsBoundsTop;

    /// <inheritdoc/>
    public double BoundsWidth => AbsBoundsRight - AbsBoundsLeft;

    /// <inheritdoc/>
    public double BoundsHeight => AbsBoundsBottom - AbsBoundsTop;

    /// <summary>Rectangle, Ellipse, and Arrow annotations support resize via point scaling.</summary>
    public bool CanResize => Type is "Rectangle" or "Ellipse" or "Arrow";

    /// <summary>
    /// Shifts the annotation so its visual bounding box top-left is at (canvasX, canvasY).
    /// Adjusts CanvasX/Y so AbsBoundsLeft/Top == the requested position.
    /// </summary>
    public void MoveTo(double canvasX, double canvasY)
    {
        double localMinX = _bboxMinX == double.MaxValue ? 0 : _bboxMinX;
        double localMinY = _bboxMinY == double.MaxValue ? 0 : _bboxMinY;
        CanvasX = canvasX - localMinX;
        CanvasY = canvasY - localMinY;
    }

    /// <summary>
    /// Scales all points so the bounding box reaches the requested size,
    /// keeping the specified anchor corner fixed in canvas space.
    /// Only meaningful for resizable annotation types (CanResize == true).
    /// No-op when the current bounding box is zero-size or has no points.
    /// </summary>
    public void ResizeTo(double newWidth, double newHeight, CGReferenceBoard.Models.Transforms.TransformAnchor anchor)
    {
        double oldW = BoundsWidth;
        double oldH = BoundsHeight;
        if (oldW <= 0 || oldH <= 0 || Points.Count == 0) return;

        double scaleX = newWidth  / oldW;
        double scaleY = newHeight / oldH;

        // Choose the pivot point in local coordinates based on which anchor stays fixed.
        double pivotLocalX = anchor switch
        {
            CGReferenceBoard.Models.Transforms.TransformAnchor.TopLeft
            or CGReferenceBoard.Models.Transforms.TransformAnchor.Left
            or CGReferenceBoard.Models.Transforms.TransformAnchor.BottomLeft => _bboxMaxX,
            CGReferenceBoard.Models.Transforms.TransformAnchor.TopRight
            or CGReferenceBoard.Models.Transforms.TransformAnchor.Right
            or CGReferenceBoard.Models.Transforms.TransformAnchor.BottomRight => _bboxMinX,
            _ => (_bboxMinX + _bboxMaxX) / 2.0,
        };
        double pivotLocalY = anchor switch
        {
            CGReferenceBoard.Models.Transforms.TransformAnchor.TopLeft
            or CGReferenceBoard.Models.Transforms.TransformAnchor.Top
            or CGReferenceBoard.Models.Transforms.TransformAnchor.TopRight => _bboxMaxY,
            CGReferenceBoard.Models.Transforms.TransformAnchor.BottomLeft
            or CGReferenceBoard.Models.Transforms.TransformAnchor.Bottom
            or CGReferenceBoard.Models.Transforms.TransformAnchor.BottomRight => _bboxMinY,
            _ => (_bboxMinY + _bboxMaxY) / 2.0,
        };

        for (int i = 0; i < Points.Count; i++)
        {
            double newX = pivotLocalX + (Points[i].X - pivotLocalX) * scaleX;
            double newY = pivotLocalY + (Points[i].Y - pivotLocalY) * scaleY;
            Points[i] = new Avalonia.Point(newX, newY);
        }
        UpdateBoundsCache();
    }

    #endregion
}
