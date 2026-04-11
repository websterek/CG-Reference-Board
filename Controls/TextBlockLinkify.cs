using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace CGReferenceBoard.Controls;

/// <summary>
/// Attached property that auto-detects URLs in text and renders them as clickable hyperlinks.
/// Usage: <c>local:TextBlockLinkify.Text="{Binding SomeText}"</c>
/// </summary>
public class TextBlockLinkify
{
    private static readonly Regex UrlRegex = new(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static readonly AttachedProperty<string> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlockLinkify, TextBlock, string>("Text");

    public static string GetText(TextBlock element) => element.GetValue(TextProperty);
    public static void SetText(TextBlock element, string value) => element.SetValue(TextProperty, value);

    static TextBlockLinkify()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>(OnTextChanged);
    }

    private static void OnTextChanged(TextBlock element, AvaloniaPropertyChangedEventArgs e)
    {
        string text = e.NewValue as string ?? string.Empty;

        element.Inlines ??= new InlineCollection();
        element.Inlines.Clear();

        if (string.IsNullOrEmpty(text))
        {
            element.Text = text;
            return;
        }

        var matches = UrlRegex.Matches(text);
        if (matches.Count == 0)
        {
            element.Text = text;
            return;
        }

        // Clear plain text to use inlines instead
        element.Text = string.Empty;
        int lastIndex = 0;

        foreach (Match match in matches)
        {
            // Add preceding non-URL text
            if (match.Index > lastIndex)
                element.Inlines.Add(new Run { Text = text[lastIndex..match.Index] });

            // Add clickable URL
            string url = match.Value;
            var linkText = new TextBlock
            {
                Text = url,
                Foreground = Brushes.LightSkyBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            linkText.PointerPressed += (_, args) =>
            {
                if (!args.GetCurrentPoint(element).Properties.IsLeftButtonPressed)
                    return;

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    args.Handled = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open URL: {ex.Message}");
                }
            };

            element.Inlines.Add(new InlineUIContainer(linkText));
            lastIndex = match.Index + match.Length;
        }

        // Add trailing non-URL text
        if (lastIndex < text.Length)
            element.Inlines.Add(new Run { Text = text[lastIndex..] });
    }
}
