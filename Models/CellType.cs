namespace CGReferenceBoard.Models;

/// <summary>
/// Defines the content types that can be placed on the reference board.
/// </summary>
public enum CellType
{
    /// <summary>Empty cell with no content.</summary>
    None,
    /// <summary>An image file (PNG, JPG, etc.).</summary>
    Image,
    /// <summary>A text note.</summary>
    Text,
    /// <summary>A video file with thumbnail.</summary>
    Video,
    /// <summary>A floating label overlaid on the board.</summary>
    Label,
    /// <summary>A colored background region for grouping cells.</summary>
    Backdrop
}
