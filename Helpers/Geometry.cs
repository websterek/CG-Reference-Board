using System;
using Avalonia;

namespace CGReferenceBoard.Helpers;

/// <summary>Pure geometry utilities. No Avalonia UI dependencies beyond Point/Rect types.</summary>
public static class GeometryHelper
{
    /// <summary>
    /// Returns the minimum distance from point <paramref name="p"/> to
    /// the line segment defined by endpoints <paramref name="v"/> and <paramref name="w"/>.
    /// </summary>
    public static double DistanceToSegment(Point p, Point v, Point w)
    {
        double l2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
        if (l2 == 0)
            return Math.Sqrt(Math.Pow(p.X - v.X, 2) + Math.Pow(p.Y - v.Y, 2));
        double t = Math.Max(0, Math.Min(1,
            ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2));
        var projection = new Point(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
        return Math.Sqrt(Math.Pow(p.X - projection.X, 2) + Math.Pow(p.Y - projection.Y, 2));
    }
}
