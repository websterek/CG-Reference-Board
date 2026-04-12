using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CGReferenceBoard.Helpers;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Controls;

/// <summary>
/// Custom control that renders an <see cref="AnnotationViewModel"/> as a drawn shape
/// (pencil stroke, rectangle, ellipse, arrow, or text) on the annotation layer.
/// </summary>
public class AnnotationShape : Control
{
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

        double pad = vm.Thickness + 4;
        Width = (maxX - _minX) + pad * 2;
        Height = (maxY - _minY) + pad * 2;
        Margin = new Thickness(_minX - pad, _minY - pad, 0, 0);
    }

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

        // Map absolute points to local coordinate space
        double offsetX = Bounds.X;
        double offsetY = Bounds.Y;

        switch (vm.Type)
        {
            case "Pencil":
                RenderPencil(context, vm, pen, selectPen, hitTestPen, offsetX, offsetY);
                break;
            case "Rectangle":
                RenderRectangle(context, vm, pen, selectPen, hitTestPen, offsetX, offsetY);
                break;
            case "Ellipse":
                RenderEllipse(context, vm, pen, selectPen, hitTestPen, offsetX, offsetY);
                break;
            case "Arrow":
                RenderArrow(context, vm, pen, selectPen, hitTestPen, offsetX, offsetY);
                break;
            case "Text":
                RenderText(context, vm, brush, selectPen, hitTestPen, offsetX, offsetY);
                break;
        }
    }

    private static void RenderPencil(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;

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

        ctx.DrawGeometry(null, hitTestPen, geometry);
        if (selectPen != null)
            ctx.DrawGeometry(null, selectPen, geometry);
        ctx.DrawGeometry(null, pen, geometry);
    }

    private static void RenderRectangle(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        ctx.DrawRectangle(Brushes.Transparent, hitTestPen, rect);
        if (selectPen != null)
            ctx.DrawRectangle(null, selectPen, rect);
        ctx.DrawRectangle(null, pen, rect);
    }

    private static void RenderEllipse(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);
        var rect = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        var center = rect.Center;
        ctx.DrawEllipse(Brushes.Transparent, hitTestPen, center, rect.Width / 2, rect.Height / 2);
        if (selectPen != null)
            ctx.DrawEllipse(null, selectPen, center, rect.Width / 2, rect.Height / 2);
        ctx.DrawEllipse(null, pen, center, rect.Width / 2, rect.Height / 2);
    }

    private static void RenderArrow(DrawingContext ctx, AnnotationViewModel vm, Pen pen, Pen? selectPen, Pen hitTestPen, double ox, double oy)
    {
        if (vm.Points.Count < 2)
            return;
        var start = new Point(vm.Points[0].X - ox, vm.Points[0].Y - oy);
        var end = new Point(vm.Points[^1].X - ox, vm.Points[^1].Y - oy);

        ctx.DrawLine(hitTestPen, start, end);
        if (selectPen != null)
            ctx.DrawLine(selectPen, start, end);
        ctx.DrawLine(pen, start, end);

        // Arrowhead
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double headLen = vm.Thickness * 3 + 5;
        var h1 = new Point(end.X - headLen * Math.Cos(angle - Math.PI / 6), end.Y - headLen * Math.Sin(angle - Math.PI / 6));
        var h2 = new Point(end.X - headLen * Math.Cos(angle + Math.PI / 6), end.Y - headLen * Math.Sin(angle + Math.PI / 6));

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

    private static void RenderText(DrawingContext ctx, AnnotationViewModel vm, IBrush brush, Pen? selectPen, Pen hitTestPen, double ox, double oy)
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

        if (selectPen != null)
        {
            ctx.DrawRectangle(null, new Pen(SolidColorBrush.Parse(Constants.AccentColor), 2), textRect);
        }
        ctx.DrawText(ft, start);
    }
}
