namespace CGReferenceBoard.Helpers;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class Constants
{
    /// <summary>Size in pixels of one grid cell.</summary>
    public const double GridSize = 160.0;

    /// <summary>Extra padding applied around Backdrop-type cells.</summary>
    public const double BackdropPadding = 80.0;

    /// <summary>Maximum number of recently opened boards to remember.</summary>
    public const int MaxRecentBoards = 5;

    /// <summary>Application display name.</summary>
    public const string AppName = "CG Reference Board";

    /// <summary>Application version string. Bump this on every release.</summary>
    public const string AppVersion = "0.9.1";

    /// <summary>Primary accent colour used for selection highlights and interactive chrome.</summary>
    public const string AccentColor = "#FF44AAFF";

    /// <summary>Name of the application config directory inside the user profile.</summary>
    public const string ConfigDirName = "CGReferenceBoard";

    /// <summary>File name for the recent boards list.</summary>
    public const string RecentBoardsFileName = "recent_boards.json";

    /// <summary>Default file extension for board files.</summary>
    public const string DefaultBoardExtension = ".cgrb";

    /// <summary>Minimum allowed zoom scale.</summary>
    public const double MinZoom = 0.05;

    /// <summary>Maximum allowed zoom scale.</summary>
    public const double MaxZoom = 5.0;

    /// <summary>Zoom increment per scroll step.</summary>
    public const double ZoomStep = 0.1;

    /// <summary>
    /// Sensitivity for middle-button drag-to-zoom (Nuke-style).
    /// Higher values make the zoom respond faster to vertical mouse movement.
    /// </summary>
    public const double MiddleZoomSensitivity = 0.0018;

    /// <summary>
    /// Maximum absolute scale change allowed per pointer-move frame for drag-to-zoom.
    /// Prevents sudden jumps when the pointer moves a large distance between frames.
    /// </summary>
    public const double MiddleZoomMaxDelta = 0.08;

    /// <summary>
    /// Minimum vertical distance in screen pixels the cursor must travel before
    /// drag-to-zoom activates. Prevents jitter when the tablet pen first touches.
    /// </summary>
    public const double MiddleZoomDeadZone = 8.0;

    /// <summary>Minimum drag distance in pixels before initiating a drag operation.</summary>
    public const double DragThreshold = 3.0;
}
