using System;
using Avalonia;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Services.Transform;

public static class TransformMath
{
    public static double SnapToGrid(double value)
        => Math.Round(value / Constants.GridSize) * Constants.GridSize;

    public static Vector SnapVectorToGrid(Vector value)
        => new(SnapToGrid(value.X), SnapToGrid(value.Y));

    public static Rect SnapRectToGrid(Rect rect)
    {
        var left = SnapToGrid(rect.Left);
        var top = SnapToGrid(rect.Top);
        var right = SnapToGrid(rect.Right);
        var bottom = SnapToGrid(rect.Bottom);

        if (right <= left)
        {
            right = left + Constants.GridSize;
        }

        if (bottom <= top)
        {
            bottom = top + Constants.GridSize;
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    public static Rect ResizeBounds(Rect original, TransformHandle handle, Vector delta, double minSize)
    {
        var left = original.Left;
        var top = original.Top;
        var right = original.Right;
        var bottom = original.Bottom;
        var clampedMinSize = Math.Max(0, minSize);

        switch (handle)
        {
            case TransformHandle.TopLeft:
                left += delta.X;
                top += delta.Y;
                break;
            case TransformHandle.Top:
                top += delta.Y;
                break;
            case TransformHandle.TopRight:
                right += delta.X;
                top += delta.Y;
                break;
            case TransformHandle.Right:
                right += delta.X;
                break;
            case TransformHandle.BottomRight:
                right += delta.X;
                bottom += delta.Y;
                break;
            case TransformHandle.Bottom:
                bottom += delta.Y;
                break;
            case TransformHandle.BottomLeft:
                left += delta.X;
                bottom += delta.Y;
                break;
            case TransformHandle.Left:
                left += delta.X;
                break;
            default:
                return original;
        }

        if (AffectsLeft(handle))
        {
            left = Math.Min(left, right - clampedMinSize);
        }
        else if (AffectsRight(handle))
        {
            right = Math.Max(right, left + clampedMinSize);
        }

        if (AffectsTop(handle))
        {
            top = Math.Min(top, bottom - clampedMinSize);
        }
        else if (AffectsBottom(handle))
        {
            bottom = Math.Max(bottom, top + clampedMinSize);
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    public static Vector GetScale(Rect original, Rect resized)
        => new(GetAxisScale(original.Width, resized.Width), GetAxisScale(original.Height, resized.Height));

    public static Point MapPointBetweenRects(Point point, Rect from, Rect to)
    {
        var relativeX = GetRelativeOffset(point.X, from.X, from.Width);
        var relativeY = GetRelativeOffset(point.Y, from.Y, from.Height);

        return new Point(
            to.X + relativeX * to.Width,
            to.Y + relativeY * to.Height);
    }

    private static bool AffectsLeft(TransformHandle handle)
        => handle is TransformHandle.TopLeft or TransformHandle.Left or TransformHandle.BottomLeft;

    private static bool AffectsRight(TransformHandle handle)
        => handle is TransformHandle.TopRight or TransformHandle.Right or TransformHandle.BottomRight;

    private static bool AffectsTop(TransformHandle handle)
        => handle is TransformHandle.TopLeft or TransformHandle.Top or TransformHandle.TopRight;

    private static bool AffectsBottom(TransformHandle handle)
        => handle is TransformHandle.BottomLeft or TransformHandle.Bottom or TransformHandle.BottomRight;

    private static double GetAxisScale(double originalSize, double resizedSize)
    {
        if (Math.Abs(originalSize) < double.Epsilon)
        {
            return 1;
        }

        return resizedSize / originalSize;
    }

    private static double GetRelativeOffset(double value, double origin, double size)
    {
        if (Math.Abs(size) < double.Epsilon)
        {
            return 0;
        }

        return (value - origin) / size;
    }
}
