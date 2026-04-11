using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;

namespace CGReferenceBoard.Controls;

/// <summary>
/// Avalonia VideoView control for embedded video playback via LibVLC.
/// Supports Windows, Linux, and macOS. Reserved for future built-in player feature.
/// </summary>
public class VideoView : NativeControlHost
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    private IPlatformHandle? _platformHandle;
    private MediaPlayer? _mediaPlayer;
    private Window? _floatingContent;
    private IDisposable? _contentChangedHandler;
    private IDisposable? _isVisibleChangedHandler;
    private IDisposable? _floatingContentChangedHandler;

    #region Styled / Direct Properties

    public static readonly DirectProperty<VideoView, MediaPlayer?> MediaPlayerProperty =
        AvaloniaProperty.RegisterDirect<VideoView, MediaPlayer?>(
            nameof(MediaPlayer),
            o => o.MediaPlayer,
            (o, v) => o.MediaPlayer = v,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VideoView, object?>(nameof(Content));

    /// <summary>Gets or sets the LibVLC MediaPlayer instance.</summary>
    public MediaPlayer? MediaPlayer
    {
        get => _mediaPlayer;
        set
        {
            if (ReferenceEquals(_mediaPlayer, value))
                return;

            Detach();
            _mediaPlayer = value;
            Attach();
        }
    }

    /// <summary>Gets or sets overlay content displayed on top of the video.</summary>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    #endregion

    public VideoView()
    {
        Initialized += (_, _) => Attach();
        _contentChangedHandler = ContentProperty.Changed.AddClassHandler<VideoView>((s, _) => s.UpdateOverlayPosition());
        _isVisibleChangedHandler = IsVisibleProperty.Changed.AddClassHandler<VideoView>((s, _) => s.ShowNativeOverlay(s.IsVisible));
    }

    #region Native Handle Attachment

    private void Attach()
    {
        if (_mediaPlayer == null || _platformHandle == null)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _mediaPlayer.Hwnd = _platformHandle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _mediaPlayer.XWindow = (uint)_platformHandle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _mediaPlayer.NsObject = _platformHandle.Handle;
    }

    private void Detach()
    {
        if (_mediaPlayer == null)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _mediaPlayer.Hwnd = IntPtr.Zero;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _mediaPlayer.XWindow = 0;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _mediaPlayer.NsObject = IntPtr.Zero;
    }

    #endregion

    #region Floating Overlay Window

    private void InitializeNativeOverlay()
    {
        if (!this.IsAttachedToVisualTree() || VisualRoot is not Window visualRoot)
            return;

        if (_floatingContent == null && Content != null)
        {
            _floatingContent = new Window
            {
                WindowDecorations = WindowDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                ZIndex = int.MaxValue,
                Opacity = 1.0,
                DataContext = DataContext
            };

            _floatingContentChangedHandler = _floatingContent.Bind(
                ContentControl.ContentProperty,
                this.GetObservable(ContentProperty));

            _floatingContent.PointerEntered += ForwardPointerEvent;
            _floatingContent.PointerExited += ForwardPointerEvent;
            _floatingContent.PointerPressed += ForwardPointerEvent;
            _floatingContent.PointerReleased += ForwardPointerEvent;

            visualRoot.LayoutUpdated += OnVisualRootLayoutUpdated;
            visualRoot.PositionChanged += OnVisualRootLayoutUpdated;
        }

        ShowNativeOverlay(IsEffectivelyVisible);
    }

    private void UpdateOverlayPosition()
    {
        if (_floatingContent == null || !IsVisible)
            return;

        bool forceSetWidth = false, forceSetHeight = false;
        var topLeft = new Point();
        var child = _floatingContent.Presenter?.Child;

        if (child?.IsArrangeValid == true)
        {
            switch (child.HorizontalAlignment)
            {
                case HorizontalAlignment.Right:
                    topLeft = topLeft.WithX(Bounds.Width - _floatingContent.Bounds.Width);
                    break;
                case HorizontalAlignment.Center:
                    topLeft = topLeft.WithX((Bounds.Width - _floatingContent.Bounds.Width) / 2);
                    break;
                case HorizontalAlignment.Stretch:
                    forceSetWidth = true;
                    break;
            }

            switch (child.VerticalAlignment)
            {
                case VerticalAlignment.Bottom:
                    topLeft = topLeft.WithY(Bounds.Height - _floatingContent.Bounds.Height);
                    break;
                case VerticalAlignment.Center:
                    topLeft = topLeft.WithY((Bounds.Height - _floatingContent.Bounds.Height) / 2);
                    break;
                case VerticalAlignment.Stretch:
                    forceSetHeight = true;
                    break;
            }
        }

        _floatingContent.SizeToContent = (forceSetWidth, forceSetHeight) switch
        {
            (true, true) => SizeToContent.Manual,
            (false, true) => SizeToContent.Width,
            (true, false) => SizeToContent.Height,
            _ => SizeToContent.Manual
        };

        _floatingContent.Width = forceSetWidth ? Bounds.Width : double.NaN;
        _floatingContent.Height = forceSetHeight ? Bounds.Height : double.NaN;
        _floatingContent.MaxWidth = Bounds.Width;
        _floatingContent.MaxHeight = Bounds.Height;

        var newPosition = this.PointToScreen(topLeft);
        if (newPosition != _floatingContent.Position)
            _floatingContent.Position = newPosition;

        if (_floatingContent.Content is Visual content
            && VisualRoot is Visual root
            && child != null)
        {
            content.Clip = ComputeVisibleRegion(root, this, child.Margin);
        }
    }

    private static RectangleGeometry? ComputeVisibleRegion(Visual parent, Visual child, Thickness childMargin)
    {
        var childPosition = child.TranslatePoint(new Point(0, 0), parent);
        if (!childPosition.HasValue) return null;

        double topDist = childPosition.Value.Y + childMargin.Top;
        double leftDist = childPosition.Value.X + childMargin.Left;
        double bottomDist = parent.Bounds.Height - (childPosition.Value.Y + child.Bounds.Height + childMargin.Bottom);
        double rightDist = parent.Bounds.Width - (childPosition.Value.X + child.Bounds.Width + childMargin.Right);

        var region = new Rect(0, 0, child.Bounds.Width, child.Bounds.Height);

        if (topDist < 0) region = new Rect(region.X, region.Y - topDist, region.Width, region.Height + topDist);
        if (leftDist < 0) region = new Rect(region.X - leftDist, region.Y, region.Width + leftDist, region.Height);
        if (rightDist < 0) region = region.WithWidth(region.Width + rightDist);
        if (bottomDist < 0) region = region.WithHeight(region.Height + bottomDist);

        return new RectangleGeometry(region);
    }

    private void ShowNativeOverlay(bool show)
    {
        if (_floatingContent == null || _floatingContent.IsVisible == show || VisualRoot is not Window visualRoot)
            return;

        if (show && this.IsAttachedToVisualTree())
            _floatingContent.Show(visualRoot);
        else
            _floatingContent.Hide();
    }

    private void OnVisualRootLayoutUpdated(object? sender, EventArgs e) => UpdateOverlayPosition();
    private void ForwardPointerEvent(object? sender, PointerEventArgs e) => RaiseEvent(e);

    #endregion

    #region Visual Tree Lifecycle

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var parent = this.GetVisualParent();
        if (parent != null)
            parent.DetachedFromVisualTree += OnParentDetachedFromVisualTree;

        base.OnAttachedToVisualTree(e);
        InitializeNativeOverlay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var parent = this.GetVisualParent();
        if (parent != null)
            parent.DetachedFromVisualTree -= OnParentDetachedFromVisualTree;

        base.OnDetachedFromVisualTree(e);
        ShowNativeOverlay(false);
    }

    private void OnParentDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (VisualRoot is not Window visualRoot) return;
        visualRoot.LayoutUpdated -= OnVisualRootLayoutUpdated;
        visualRoot.PositionChanged -= OnVisualRootLayoutUpdated;
    }

    #endregion

    #region Native Control Core

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _platformHandle = base.CreateNativeControlCore(parent);

        if (_platformHandle.Handle != IntPtr.Zero && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnsureClipChildren(_platformHandle.Handle);

        Attach();
        return _platformHandle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _contentChangedHandler?.Dispose();
        _isVisibleChangedHandler?.Dispose();
        _floatingContentChangedHandler?.Dispose();

        Detach();

        if (_floatingContent != null)
        {
            _floatingContent.PointerEntered -= ForwardPointerEvent;
            _floatingContent.PointerExited -= ForwardPointerEvent;
            _floatingContent.PointerPressed -= ForwardPointerEvent;
            _floatingContent.PointerReleased -= ForwardPointerEvent;
            _floatingContent.Close();
            _floatingContent = null;
        }

        base.DestroyNativeControlCore(control);
        _platformHandle = null;
    }

    private static void EnsureClipChildren(IntPtr hwnd)
    {
        const int GWL_STYLE = -16;
        const uint WS_CLIPCHILDREN = 0x02000000;

        uint style = GetWindowLong(hwnd, GWL_STYLE);
        if ((style & WS_CLIPCHILDREN) == 0)
            SetWindowLong(hwnd, GWL_STYLE, style | WS_CLIPCHILDREN);
    }

    #endregion
}
