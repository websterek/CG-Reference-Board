using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CGReferenceBoard.Helpers;

namespace CGReferenceBoard.Views;

public partial class CreateDatabaseWizardDialog : Window
{
    private string? _selectedLocation;
    private bool _hasExistingBoards;
    private bool _canClose;

    public string? DatabasePath { get; private set; }
    public string? BoardPath { get; private set; }

    public CreateDatabaseWizardDialog()
    {
        InitializeComponent();
        NameTextBox.TextChanged += ValidateForm;
        BoardNameTextBox.TextChanged += ValidateForm;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_canClose)
            e.Cancel = true;
    }

    private async void BrowseLocation_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Database Location",
            AllowMultiple = false
        });

        if (folders is { Count: > 0 })
        {
            _selectedLocation = folders[0].Path.LocalPath;
            LocationTextBox.Text = _selectedLocation;
            CheckForExistingBoards();
            ValidateForm();
        }
    }

    private void CheckForExistingBoards()
    {
        if (string.IsNullOrEmpty(_selectedLocation) || !Directory.Exists(_selectedLocation))
        {
            _hasExistingBoards = false;
            ExistingDatabaseWarning.IsVisible = false;
            return;
        }

        try
        {
            var existingBoards = Directory.GetFiles(_selectedLocation, $"*{Constants.DefaultBoardExtension}")
                .Concat(Directory.GetFiles(_selectedLocation, "*.json"))
                .Any();

            _hasExistingBoards = existingBoards;
            ExistingDatabaseWarning.IsVisible = _hasExistingBoards;
        }
        catch
        {
            _hasExistingBoards = false;
            ExistingDatabaseWarning.IsVisible = false;
        }
    }

    private void ValidateForm(object? sender = null, EventArgs? e = null)
    {
        bool isValid = !string.IsNullOrWhiteSpace(_selectedLocation)
                    && !string.IsNullOrWhiteSpace(NameTextBox.Text)
                    && !string.IsNullOrWhiteSpace(BoardNameTextBox.Text);

        CreateButton.IsEnabled = isValid;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _canClose = true;
        Close();
    }

    private void Create_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedLocation)
            || string.IsNullOrWhiteSpace(NameTextBox.Text)
            || string.IsNullOrWhiteSpace(BoardNameTextBox.Text))
        {
            return;
        }

        var dbName = NameTextBox.Text.Trim();
        var boardName = BoardNameTextBox.Text.Trim();
        var dbPath = Path.Combine(_selectedLocation, dbName);

        if (!Directory.Exists(dbPath))
        {
            Directory.CreateDirectory(dbPath);
        }

        var imagesPath = Path.Combine(dbPath, "images");
        var videosPath = Path.Combine(dbPath, "videos");

        if (!Directory.Exists(imagesPath))
        {
            Directory.CreateDirectory(imagesPath);
        }

        if (!Directory.Exists(videosPath))
        {
            Directory.CreateDirectory(videosPath);
        }

        var boardFileName = boardName.EndsWith(Constants.DefaultBoardExtension)
            ? boardName
            : boardName + Constants.DefaultBoardExtension;

        BoardPath = Path.Combine(dbPath, boardFileName);
        DatabasePath = dbPath;

        _canClose = true;
        Close(true);
    }
}