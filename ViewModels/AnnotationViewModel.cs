using System;
using Avalonia;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a single annotation (drawing, shape, or text note) on the board's annotation layer.
/// </summary>
public partial class AnnotationViewModel : ViewModelBase
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

    /// <summary>
    /// Whether this annotation overlaps the current viewport.
    /// Set by the LOD timer; drives IsVisible on the AnnotationShape so
    /// off-screen annotations are excluded from layout and rendering.
    /// Defaults to true so annotations are visible before the first LOD pass.
    /// </summary>
    [ObservableProperty]
    private bool _isInViewport = true;

    [ObservableProperty]
    private bool _isHitTestEnabled = true;

    /// <summary>Canvas X offset for the entire annotation group.</summary>
    [ObservableProperty]
    private double _canvasX;

    /// <summary>Canvas Y offset for the entire annotation group.</summary>
    [ObservableProperty]
    private double _canvasY;

    /// <summary>Stroke/fill color in ARGB hex format.</summary>
    [ObservableProperty]
    private string _color = "#FFFF4444";

    /// <summary>Stroke thickness in pixels.</summary>
    [ObservableProperty]
    private double _thickness = 4.0;

    /// <summary>Ordered collection of points that define the annotation shape.</summary>
    public ObservableCollection<Point> Points { get; set; } = new();

    /// <summary>Annotation tool type: Brush, Rectangle, Ellipse, Arrow, or Text.</summary>
    [ObservableProperty]
    private string _type = "Brush";

    /// <summary>Text content (only used when <see cref="Type"/> is "Text").</summary>
    [ObservableProperty]
    private string _text = "";

    /// <summary>Additional scale factor applied to text annotations during resize.</summary>
    [ObservableProperty]
    private double _textScale = 1.0;

    /// <summary>Whether this annotation is currently selected for moving/editing.</summary>
    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(IsHitTestable));

    /// <summary>Whether the application is currently in draw/annotation mode.</summary>
    [ObservableProperty]
    private bool _isInDrawMode;

    partial void OnIsInDrawModeChanged(bool value) => OnPropertyChanged(nameof(IsHitTestable));

    /// <summary>
    /// Whether this annotation should receive pointer events.
    /// True when in draw mode OR when the annotation is selected.
    /// </summary>
    public bool IsHitTestable => IsInDrawMode || IsSelected;

    public static double GetTextFontSize(AnnotationViewModel annotation)
        => Math.Max(12, (annotation.Thickness * 4 + 10) * annotation.TextScale);
}
