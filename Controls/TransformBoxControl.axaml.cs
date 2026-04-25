using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CGReferenceBoard.Models.Transforms;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Controls;

/// <summary>
/// An overlay control that renders a dashed selection border and up to nine
/// interactive handles (eight resize + one centre-move) over the current
/// selection, delegating all transform logic to
/// <see cref="TransformAdornerViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// Position the control on the canvas with
/// <c>Canvas.Left="{Binding Adorner.Left}"</c>,
/// <c>Canvas.Top="{Binding Adorner.Top}"</c>,
/// <c>Width="{Binding Adorner.Width}"</c>, and
/// <c>Height="{Binding Adorner.Height}"</c> from the parent window, and set
/// <c>DataContext="{Binding Adorner}"</c>.
/// </para>
/// <para>
/// The control subscribes to its own <see cref="Control.SizeChanged"/> event
/// to reposition all handle thumbs whenever the bounding box changes.
/// </para>
/// </remarks>
public partial class TransformBoxControl : UserControl
{
    private const double HandleSize = 8.0;
    private const double HandleHalf = HandleSize / 2.0;

    // ── Drag state ────────────────────────────────────────────────────────────

    private TransformAnchor _dragAnchor;
    private double _dragStartLeft;
    private double _dragStartTop;
    private double _dragStartWidth;
    private double _dragStartHeight;
    private double _accX;
    private double _accY;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initialises the control and wires up handle drag events.</summary>
    public TransformBoxControl()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;

        // Batch-wire all handles to the shared drag handlers.
        IEnumerable<Thumb> handles =
        [
            Handle_Center,
            Handle_TopLeft, Handle_Top,    Handle_TopRight,
            Handle_Right,   Handle_BottomRight,
            Handle_Bottom,  Handle_BottomLeft, Handle_Left,
        ];

        foreach (var handle in handles)
        {
            handle.DragStarted += Handle_DragStarted;
            handle.DragDelta   += Handle_DragDelta;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private TransformAdornerViewModel? Adorner => DataContext as TransformAdornerViewModel;

    // ── Size change → reposition all handles ─────────────────────────────────

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        PositionAllHandles(e.NewSize.Width, e.NewSize.Height);

    private void PositionAllHandles(double w, double h)
    {
        // Dashed selection border fills the full control area.
        PART_Border.Width  = w;
        PART_Border.Height = h;
        Canvas.SetLeft(PART_Border, 0);
        Canvas.SetTop(PART_Border,  0);

        // Handles are centred on their respective edge/corner positions.
        SetHandle(Handle_TopLeft,     0,   0);
        SetHandle(Handle_Top,         w/2, 0);
        SetHandle(Handle_TopRight,    w,   0);
        SetHandle(Handle_Right,       w,   h/2);
        SetHandle(Handle_BottomRight, w,   h);
        SetHandle(Handle_Bottom,      w/2, h);
        SetHandle(Handle_BottomLeft,  0,   h);
        SetHandle(Handle_Left,        0,   h/2);
        SetHandle(Handle_Center,      w/2, h/2);
    }

    private static void SetHandle(Thumb handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - HandleHalf);
        Canvas.SetTop(handle,  y - HandleHalf);
    }

    // ── Drag handlers ─────────────────────────────────────────────────────────

    private void Handle_DragStarted(object? sender, VectorEventArgs e)
    {
        var adorner = Adorner;
        if (adorner == null) return;

        // Resolve the anchor from the Thumb's Tag string.
        _dragAnchor = sender is Thumb { Tag: string tag } && Enum.TryParse(tag, out TransformAnchor parsed)
            ? parsed
            : TransformAnchor.None;

        // Snapshot the union-box dimensions at the start of the drag.
        _dragStartLeft   = adorner.Left;
        _dragStartTop    = adorner.Top;
        _dragStartWidth  = adorner.Width;
        _dragStartHeight = adorner.Height;
        _accX = 0;
        _accY = 0;
    }

    private void Handle_DragDelta(object? sender, VectorEventArgs e)
    {
        var adorner = Adorner;
        if (adorner == null) return;

        // Accumulate delta from start so grid-snapping doesn't reset the origin.
        _accX += e.Vector.X;
        _accY += e.Vector.Y;

        // Compute the desired new geometry based on which handle is dragged.
        double newLeft   = _dragStartLeft;
        double newTop    = _dragStartTop;
        double newWidth  = _dragStartWidth;
        double newHeight = _dragStartHeight;

        switch (_dragAnchor)
        {
            case TransformAnchor.None:
                // Centre handle: pure move.
                newLeft = _dragStartLeft + _accX;
                newTop  = _dragStartTop  + _accY;
                break;

            case TransformAnchor.TopLeft:
                newLeft   = _dragStartLeft   + _accX;
                newTop    = _dragStartTop    + _accY;
                newWidth  = _dragStartWidth  - _accX;
                newHeight = _dragStartHeight - _accY;
                break;

            case TransformAnchor.Top:
                newTop    = _dragStartTop    + _accY;
                newHeight = _dragStartHeight - _accY;
                break;

            case TransformAnchor.TopRight:
                newTop    = _dragStartTop    + _accY;
                newWidth  = _dragStartWidth  + _accX;
                newHeight = _dragStartHeight - _accY;
                break;

            case TransformAnchor.Right:
                newWidth = _dragStartWidth + _accX;
                break;

            case TransformAnchor.BottomRight:
                newWidth  = _dragStartWidth  + _accX;
                newHeight = _dragStartHeight + _accY;
                break;

            case TransformAnchor.Bottom:
                newHeight = _dragStartHeight + _accY;
                break;

            case TransformAnchor.BottomLeft:
                newLeft   = _dragStartLeft   + _accX;
                newWidth  = _dragStartWidth  - _accX;
                newHeight = _dragStartHeight + _accY;
                break;

            case TransformAnchor.Left:
                newLeft  = _dragStartLeft  + _accX;
                newWidth = _dragStartWidth - _accX;
                break;
        }

        adorner.ApplyTransform(_dragAnchor, newLeft, newTop, newWidth, newHeight);
    }
}
