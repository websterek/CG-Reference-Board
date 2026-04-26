using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CGReferenceBoard.Modes;

namespace CGReferenceBoard.Services.Transform;

public sealed partial class TransformService : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private Rect _bounds;

    [ObservableProperty]
    private TransformOperation _operation;

    [ObservableProperty]
    private TransformHandle _activeHandle;

    [ObservableProperty]
    private TransformCapabilities _capabilities = TransformCapabilities.None;

    [ObservableProperty]
    private IReadOnlyList<TransformItemSnapshot> _activeSnapshots = Array.Empty<TransformItemSnapshot>();

    [ObservableProperty]
    private Rect _startBounds;

    [ObservableProperty]
    private Point _startPointer;

    public bool HasActiveOperation => Operation != TransformOperation.None;

    public void Refresh(SelectionService selection, ModeService modeService, bool isViewMode)
    {
        if (HasActiveOperation)
        {
            return;
        }

        var capabilities = GetCapabilities(selection, modeService, isViewMode);
        var snapshots = capabilities == TransformCapabilities.None
            ? Array.Empty<TransformItemSnapshot>()
            : TransformBoundsCalculator.CreateSnapshots(selection.SelectedCells, selection.SelectedAnnotations);
        var bounds = snapshots.Count > 0 ? GetSelectionBounds(snapshots) : default;

        Capabilities = snapshots.Count > 0 ? capabilities : TransformCapabilities.None;
        ActiveSnapshots = snapshots;
        Bounds = bounds;
        IsVisible = snapshots.Count > 0;
        Operation = TransformOperation.None;
        ActiveHandle = TransformHandle.None;
        OnPropertyChanged(nameof(HasActiveOperation));
    }

    public void BeginMove(Point pointer, SelectionService selection, IReadOnlyList<TransformItemSnapshot>? snapshots = null, Rect? boundsOverride = null)
        => Begin(TransformOperation.Move, TransformHandle.Body, pointer, selection, snapshots, boundsOverride);

    public void BeginResize(TransformHandle handle, Point pointer, SelectionService selection)
        => Begin(TransformOperation.Resize, handle, pointer, selection, snapshots: null);

    public Vector UpdatePreview(Point pointer, bool annotationMode)
    {
        if (!HasActiveOperation)
        {
            return default;
        }

        var delta = pointer - StartPointer;
        if (Operation == TransformOperation.Move)
        {
            var previewDelta = annotationMode ? delta : TransformMath.SnapVectorToGrid(delta);
            Bounds = StartBounds.Translate(previewDelta);
            return previewDelta;
        }

        var resizedBounds = TransformMath.ResizeBounds(StartBounds, ActiveHandle, delta, annotationMode ? 1 : 0);
        Bounds = annotationMode ? resizedBounds : TransformMath.SnapRectToGrid(resizedBounds);
        return delta;
    }

    public void End()
    {
        Operation = TransformOperation.None;
        ActiveHandle = TransformHandle.None;
        StartBounds = default;
        StartPointer = default;
        OnPropertyChanged(nameof(HasActiveOperation));
    }

    public void ClearSnapshots()
    {
        ActiveSnapshots = Array.Empty<TransformItemSnapshot>();
    }

    public void Cancel()
    {
        End();
        ClearSnapshots();
    }

    private void Begin(
        TransformOperation operation,
        TransformHandle handle,
        Point pointer,
        SelectionService selection,
        IReadOnlyList<TransformItemSnapshot>? snapshots,
        Rect? boundsOverride = null)
    {
        if (!Capabilities.AllowsOperation(operation))
        {
            return;
        }

        snapshots ??= TransformBoundsCalculator.CreateSnapshots(selection.SelectedCells, selection.SelectedAnnotations);
        if (snapshots.Count == 0)
        {
            End();
            IsVisible = false;
            Bounds = default;
            Capabilities = TransformCapabilities.None;
            return;
        }

        ActiveSnapshots = snapshots;
        // Use the provided bounds override (e.g. from the pre-drag Refresh state) so the
        // selection rect doesn't jump when CreateExpandedMoveSnapshots adds extra items
        // (such as annotations overlapping the selected cell).
        StartBounds = boundsOverride ?? GetSelectionBounds(snapshots);
        Bounds = StartBounds;
        StartPointer = pointer;
        Operation = operation;
        ActiveHandle = handle;
        IsVisible = true;
        OnPropertyChanged(nameof(HasActiveOperation));
    }

    private static TransformCapabilities GetCapabilities(SelectionService selection, ModeService modeService, bool isViewMode)
    {
        if (isViewMode)
        {
            return TransformCapabilities.None;
        }

        if (modeService.IsGridMode)
        {
            bool hasCells = selection.SelectedCells.Count > 0;
            bool hasAnnotations = selection.SelectedAnnotations.Count > 0;

            if (!hasCells && !hasAnnotations)
            {
                return TransformCapabilities.None;
            }

            return new TransformCapabilities(
                CanMove: true,
                CanResize: hasCells && !hasAnnotations,
                UsesGridSnapping: true,
                UsesCollisionChecks: true);
        }

        return modeService.AnnotationMode.IsMoveMode
            ? TransformCapabilities.Annotation
            : TransformCapabilities.None;
    }

    private static Rect GetSelectionBounds(IReadOnlyList<TransformItemSnapshot> snapshots)
    {
        var bounds = snapshots[0].Bounds;
        for (int i = 1; i < snapshots.Count; i++)
        {
            bounds = Union(bounds, snapshots[i].Bounds);
        }

        return bounds;
    }

    private static Rect Union(Rect left, Rect right)
    {
        var x1 = Math.Min(left.X, right.X);
        var y1 = Math.Min(left.Y, right.Y);
        var x2 = Math.Max(left.Right, right.Right);
        var y2 = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
}
