using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace CGReferenceBoard.Views;

/// <summary>
/// A reusable toast notification control that auto-fades after a short duration.
/// Place anywhere in the visual tree — it manages its own visibility and opacity.
/// </summary>
public partial class ToastNotification : UserControl
{
    private CancellationTokenSource? _toastCts;

    public ToastNotification()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Displays <paramref name="message"/> for 1.5 seconds, then fades out.
    /// Cancels any previously active toast.
    /// </summary>
    public async Task Show(string message)
    {
        var border = this.FindControl<Border>("ToastBorder");
        var text = this.FindControl<TextBlock>("ToastText");
        if (border == null || text == null)
            return;

        _toastCts?.CancelAsync();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;

        text.Text = message;
        border.IsVisible = true;
        border.Opacity = 1;

        try
        {
            await Task.Delay(1500, token);
            border.Opacity = 0;
            await Task.Delay(250, token);
            border.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>
    /// Immediately hides the toast.
    /// </summary>
    public void Hide()
    {
        _toastCts?.CancelAsync();
        var border = this.FindControl<Border>("ToastBorder");
        if (border != null)
        {
            border.Opacity = 0;
            border.IsVisible = false;
        }
    }
}
