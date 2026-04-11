namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Represents a board file entry in the Board menu.
/// </summary>
public class BoardMenuItemViewModel : ViewModelBase
{
    private string _fileName = "";
    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
