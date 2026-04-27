using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CGReferenceBoard.Views;

public partial class TextInputDialog : Window, INotifyPropertyChanged
{
    private string _dialogTitle = "New Board";
    public string DialogTitle
    {
        get => _dialogTitle;
        set { _dialogTitle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DialogTitle))); }
    }

    private string _inputText = "";
    public string InputText
    {
        get => _inputText;
        set { _inputText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputText))); }
    }

    private bool _canClose;

    public string Result { get; private set; } = "";

    public new event PropertyChangedEventHandler? PropertyChanged;

    public TextInputDialog()
    {
        InitializeComponent();
        Closing += OnClosing;
        DataContext = this;
    }

    public TextInputDialog(string title, string initialText)
    {
        DialogTitle = title;
        InputText = initialText;
        InitializeComponent();
        Closing += OnClosing;
        DataContext = this;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_canClose)
            e.Cancel = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _canClose = true;
        Close(null);
    }

    private void Create_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            Result = InputTextBox.Text.Trim();
            _canClose = true;
            Close(Result);
        }
    }
}