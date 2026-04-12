using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Controls;

/// <summary>
/// Annotation effect modes.
/// </summary>
public enum AnnotationEffect
{
    None,
    Shadow,
    Outline
}

/// <summary>
/// Custom control that renders an <see cref="AnnotationViewModel"/> as a drawn shape
/// (brush stroke, rectangle, ellipse, arrow, or text) on the annotation layer.
/// Supports optional shadow or outline effects for readability.
/// </summary>
public class AnnotationShape : Control
{
    // ───────── Static effect state ─────────

    private static AnnotationEffect _currentEffect = AnnotationEffect.None;

    /// <summary>Current global effect mode for all annotation shapes.</summary>
    public static AnnotationEffect CurrentEffect => _currentEffect;

    /// <summary>Raised when the effect mode changes so all instances can re-render.</summary>
    public static event Action? EffectModeChanged;

    /// <summary>
    /// Sets the global effect mode and notifies all instances to re-render.
    /// </summary>
    public static void SetEffectMode(AnnotationEffect mode)
    {
        if (_currentEffect == mode)
            return;
        _currentEffect = mode;
        EffectModeChanged?.Invoke();
    }

    // ───────── Instance ─────────

    public static readonly StyledProperty<AnnotationViewModel?> AnnotationProperty =
        AvaloniaProperty.Register<AnnotationShape, AnnotationViewModel?>(nameof(Annotation));

    public AnnotationViewModel? Annotation
    {
        get => GetValue(AnnotationProperty);
        set => SetValue(AnnotationProperty, value);
    }

    public AnnotationShape()
    {
        ClipToBounds = false;
        EffectModeChanged += OnEffectModeChanged;
    }

    private void OnEffectModeChanged() => InvalidateVisual();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EffectModeChanged -= OnEffectModeChanged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != AnnotationProperty)
            return;

        if (change.OldValue is AnnotationViewModel oldVm)
        {
            oldVm.Points.CollectionChanged -= OnPointsCollectionChanged;
            oldVm.PropertyChanged -= OnAnnotationPropertyChanged;
        }

        if (change.NewValue is AnnotationViewModel newVm)
        {
            newVm.Points.CollectionChanged += OnPointsCollectionChanged;
            newVm.PropertyChanged += OnAnnotationPropertyChanged;
        }

        UpdateBounds();
        InvalidateAll();
    }

    private void OnAnnotationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnnotationViewModel.Color)
            or nameof(AnnotationViewModel.Thickness)
            or nameof(AnnotationViewModel.IsSelected)
            or nameof(AnnotationViewModel.Text)
            or nameof(AnnotationViewModel.Type))
        {
            InvalidateAll();
        }
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateBounds();
        InvalidateAll();
    }

    private void InvalidateAll()
    {
        InvalidateVisual();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private double _minX, _minY;

    private void UpdateBounds()
    {
        var vm = Annotation;
        if (vm == null || vm.Points.Count == 0)
            return;

        _minX = double.MaxValue;
        _minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var pt in vm.Points)
        {
            if (pt.X < _minX)
                _minX = pt.X;
            if (pt.X > maxX)
                maxX = pt.X;
            if (pt.Y < _minY)
                _minY = pt.Y;
            if (pt.Y > maxY)
                maxY = pt.Y;
        }

        if (vm.Type == "Text")
        {
            // Reserve rough space for text to avoid clipping
            maxX = _minX + 300;
            maxY = _minY + 100;
        }

        double pad = vm.Thickness + Constants.AnnotationEffectPadding;
        Width = (maxX - _minX) + pad * 2;
        Height = (maxY - _minY) + pad * 2;
        Margin = new Thickness(_minX - pad, _minY - pad, 0, 0);
    }

    // ───────── Effect helpers ─────────

    private static readonly IBrush ShadowBrush = SolidColorBrush.Parse(Constants.AnnotationShadowColor);
    private static readonly IBrush OutlineBrush = SolidColorBrush.Parse(Constants.AnnotationOutlineColor);

    private static Pen MakeShadowPen(double thickness)
        => new(ShadowBrush, thickness + Constants.AnnotationShadowExtraThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    private static Pen MakeOutlinePen(double thickness)
        => new(OutlineBrush, thickness + Constants.AnnotationOutlineExtraThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    // ───────── Render ─────────

    public override void Render(DrawingContext context)
    {
        var vm = Annotation;
        if (vm == null || vm.Points.Count == 0)
            return;

        var brush = SolidColorBrush.Parse(vm.Color);
        var pen = new Pen(brush, vm.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var selectPen = vm.IsSelected
            ? new Pen(SolidColorBrush.Parse(Constants.AccentColor), vm.Thickness + 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
            : null;
        var hitTestPen = new Pen(Brushes.Transparent, Math.Max(20, vm.Thickness + 10), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var effect = _currentEffect;

        // Map absolute points to local coordinate space
        double offsetX = Bounds.X;
        double offsetY = Bounds.Y;

        switch (vm.Type)
        {
            case "Brush":
                RenderBrush(context, vm, pen, selectPen, hitTestPen, effect, offsetX, offsetY);
                break;
            case "Rectangle":
                RenderRectangle(context, vm, pen, selectPen, hitTestPen, effect, offsetX, offsetY);
                break;
            case "Ellipse":
                RenderEllipse(context, vm, pen, selectPen, hitTestPen, effect, offsetX, offsetY);
                break;
            case "Arrow":
                RenderArrow(context, vm, pen, selectPen, hitTestPen, effect, offsetX, offsetY);
                break;
            case "Text":
                RenderText(context, vm, brush, selectPen, hitTestPen, effect, offsetX, offsetY);
                break;
        }
    }

    // ───────── Shape renderers ─────────

    private static void RenderBrush(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;

        var geometry = BuildBrushGeometry(vm, ox, oy);

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            using (ctx.PushTransform(Matrix.CreateTranslation(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY)))
                ctx.DrawGeometry(null, MakeShadowPen(vm.Thickness), geometry);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawGeometry(null, MakeOutlinePen(vm.Thickness), geometry);
        }

        ctx.DrawGeometry(null, hitTestPen, geometry);
        if (selectPen != null)
            ctx.DrawGeometry(null, selectPen, geometry);
        ctx.DrawGeometry(null, pen, geometry);
    }

    private static StreamGeometry BuildBrushGeometry(AnnotationViewModel vm, double ox, double oy)
    {
        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            var p0 = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
            gc.BeginFigure(p0, isFilled: false);

            if (vm.Points.Count == 2)
            {
                var p1 = new Point(vm.Points[1].X - ox, vm.Points[1].Y - oy);
                gc.LineTo(p1);
            }
            else
            {
                for (int i = 1; i < vm.Points.Count - 1; i++)
                {
                    var curr = new Point(vm.Points[i].X - ox, vm.Points[i].Y - oy);
                    var next = new Point(vm.Points[i + 1].X - ox, vm.Points[i + 1].Y - oy);
                    var mid = new Point((curr.X + next.X) / 2, (curr.Y + next.Y) / 2);

                    gc.QuadraticBezierTo(curr, mid);
                }

                var last = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
                gc.LineTo(last);
            }
            gc.EndFigure(isClosed: false);
        }
        return geometry;
    }

    private static void RenderRectangle(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var shadowRect = rect.Translate(new Vector(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY));
            ctx.DrawRectangle(null, MakeShadowPen(vm.Thickness), shadowRect);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawRectangle(null, MakeOutlinePen(vm.Thickness), rect);
        }

        ctx.DrawRectangle(Brushes.Transparent, hitTestPen, rect);
        if (selectPen != null)
            ctx.DrawRectangle(null, selectPen, rect);
        ctx.DrawRectangle(null, pen, rect);
    }

    private static void RenderEllipse(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        var center = rect.Center;

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var sc = new Point(center.X + Constants.AnnotationShadowOffsetX, center.Y + Constants.AnnotationShadowOffsetY);
            ctx.DrawEllipse(null, MakeShadowPen(vm.Thickness), sc, rect.Width / 2, rect.Height / 2);
        }
        else if (effect == AnnotationEffect.Outline)
        {
            ctx.DrawEllipse(null, MakeOutlinePen(vm.Thickness), center, rect.Width / 2, rect.Height / 2);
        }

        ctx.DrawEllipse(Brushes.Transparent, hitTestPen, center, rect.Width / 2, rect.Height / 2);
        if (selectPen != null)
            ctx.DrawEllipse(null, selectPen, center, rect.Width / 2, rect.Height / 2);
        ctx.DrawEllipse(null, pen, center, rect.Width / 2, rect.Height / 2);
    }

    private static void RenderArrow(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);

        // Arrowhead points
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double headLen = vm.Thickness * 3 + 5;
        var h1 = new Point(end.X - headLen * Math.Cos(angle - Math.PI / 6), end.Y - headLen * Math.Sin(angle - Math.PI / 6));
        var h2 = new Point(end.X - headLen * Math.Cos(angle + Math.PI / 6), end.Y - headLen * Math.Sin(angle + Math.PI / 6));

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var sp = MakeShadowPen(vm.Thickness);
            using (ctx.PushTransform(Matrix.CreateTranslation(Constants.AnnotationShadowOffsetX, Constants.AnnotationShadowOffsetY)))
            {
                ctx.DrawLine(sp, start, end);
                ctx.DrawLine(sp, end, h1);
                ctx.DrawLine(sp, end, h2);
            }
        }
        else if (effect == AnnotationEffect.Outline)
        {
            var op = MakeOutlinePen(vm.Thickness);
            ctx.DrawLine(op, start, end);
            ctx.DrawLine(op, end, h1);
            ctx.DrawLine(op, end, h2);
        }

        ctx.DrawLine(hitTestPen, start, end);
        if (selectPen != null)
            ctx.DrawLine(selectPen, start, end);
        ctx.DrawLine(pen, start, end);

        ctx.DrawLine(hitTestPen, end, h1);
        ctx.DrawLine(hitTestPen, end, h2);
        if (selectPen != null)
        {
            ctx.DrawLine(selectPen, end, h1);
            ctx.DrawLine(selectPen, end, h2);
        }
        ctx.DrawLine(pen, end, h1);
        ctx.DrawLine(pen, end, h2);
    }

    private static void RenderText(DrawingContext ctx, AnnotationViewModel vm, IBrush brush, Pen? selectPen, Pen hitTestPen, AnnotationEffect effect, double ox, double oy)
    {
        if (vm.Points.Count == 0)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var typeface = new Typeface("Inter, Arial");
        double fontSize = Math.Max(12, vm.Thickness * 4 + 10);
        var ft = new FormattedText(
            vm.Text ?? "",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush);

        var textRect = new Rect(start, new Size(ft.Width, ft.Height));
        ctx.DrawRectangle(Brushes.Transparent, null, textRect);

        // Effect pass
        if (effect == AnnotationEffect.Shadow)
        {
            var shadowFt = new FormattedText(
                vm.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                ShadowBrush);
            ctx.DrawText(shadowFt, new Point(start.X + Constants.AnnotationShadowOffsetX, start.Y + Constants.AnnotationShadowOffsetY));
        }
        else if (effect == AnnotationEffect.Outline)
        {
            var outlineFt = new FormattedText(
                vm.Text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                OutlineBrush);
            // Draw at 4 cardinal + 4 diagonal offsets for a smooth outline
            double d = Constants.AnnotationTextOutlineOffset;
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y));
            ctx.DrawText(outlineFt, new Point(start.X, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X, start.Y + d));
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y - d));
            ctx.DrawText(outlineFt, new Point(start.X - d, start.Y + d));
            ctx.DrawText(outlineFt, new Point(start.X + d, start.Y + d));
        }

        if (selectPen != null)
        {
            ctx.DrawRectangle(null, new Pen(SolidColorBrush.Parse(Constants.AccentColor), 2), textRect);
        }
        ctx.DrawText(ft, start);
    }
}
