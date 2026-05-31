using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace CGReferenceBoard.Views;

public partial class FullscreenMediaOverlay : UserControl
{
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? Closed;

    public bool IsOpen => IsVisible;

    public string TextContent
    {
        get => FullText.Text;
        set => FullText.Text = value;
    }

    public FullscreenMediaOverlay()
    {
        InitializeComponent();
        FullText.TextChanged += OnFullTextChanged;
    }

    public void ShowImage(IImage? source)
    {
        FullImage.Source = source;
        FullImage.IsVisible = true;
        FullText.IsVisible = false;
        IsVisible = true;
    }

    public void ShowText(string text)
    {
        FullText.Text = text;
        FullText.IsVisible = true;
        FullImage.IsVisible = false;
        IsVisible = true;
    }

    public new void Hide()
    {
        IsVisible = false;
        FullText.IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void OnFullTextChanged(object? sender, TextChangedEventArgs e)
    {
        TextChanged?.Invoke(this, e);
    }

    private void CloseFullMedia_Click(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        Hide();
    }
}
