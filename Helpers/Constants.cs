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

    /// <summary>Maximum number of undo snapshots to keep in memory.</summary>
    public const int MaxUndoDepth = 50;

    /// <summary>Application display name.</summary>
    public const string AppName = "CG Reference Board";

    /// <summary>Application version string. Bump this on every release.</summary>
    public const string AppVersion = "0.9.7";

    /// <summary>
    /// Primary accent colour used for selection highlights and interactive chrome.
    /// NOTE: Must stay in sync with &lt;Color x:Key="AccentColor"&gt; in App.axaml (#44AAFF, same hue without the FF alpha prefix).
    /// </summary>
    public const string AccentColor = "#FF44AAFF";

    /// <summary>Name of the application config directory inside the user profile.</summary>
    public const string ConfigDirName = "CGReferenceBoard";

    /// <summary>File name for the recent boards list.</summary>
    public const string RecentBoardsFileName = "recent_boards.json";

    /// <summary>File name for per-user application settings.</summary>
    public const string UserSettingsFileName = "user_settings.json";

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

    /// <summary>Maximum spiral search distance in grid units when auto-placing cells/backdrops.</summary>
    public const int SpiralSearchMaxDistance = 20;

    // ───────── Annotation effect settings ─────────

    /// <summary>Shadow colour (ARGB hex). Semi-transparent black works on most backgrounds.</summary>
    public const string AnnotationShadowColor = "#78000000";

    /// <summary>Shadow X offset in pixels.</summary>
    public const double AnnotationShadowOffsetX = 2.0;

    /// <summary>Shadow Y offset in pixels.</summary>
    public const double AnnotationShadowOffsetY = 2.0;

    /// <summary>Extra pen thickness added to the annotation stroke for shadow rendering.</summary>
    public const double AnnotationShadowExtraThickness = 1.0;

    /// <summary>Outline colour (ARGB hex). Dark semi-transparent for contrast.</summary>
    public const string AnnotationOutlineColor = "#C8000000";

    /// <summary>Extra pen thickness added to the annotation stroke for outline rendering.</summary>
    public const double AnnotationOutlineExtraThickness = 3.0;

    /// <summary>Offset distance in pixels for each text-outline pass (cardinal + diagonal).</summary>
    public const double AnnotationTextOutlineOffset = 1.5;

    /// <summary>Extra padding around annotation shapes to avoid clipping effects.</summary>
    public const double AnnotationEffectPadding = 8.0;

    // ───────── Grid visual settings ─────────

    /// <summary>Color of the main intersection dots on the grid background.</summary>
    public const string GridDotColor = "#2D2D2D";

    /// <summary>Size in pixels of each quarter-dot at tile corners (4 combine into one dot).</summary>
    public const double GridDotSize = 1.0;

    /// <summary>Color of the grid lines drawn between intersection points.</summary>
    public const string GridLineColor = "#1E1E1E";

    /// <summary>Thickness in pixels of the grid lines.</summary>
    public const double GridLineThickness = 1.0;

    // ───────── Backdrop / Label color palettes ─────────

    /// <summary>Background colors for backdrops (semi-transparent tinted).</summary>
    public static readonly string[] BackdropBackgroundColors =
        { "#88222222", "#885A3A10", "#881A3A4A", "#881A4A2A", "#884A1A2A", "#88444444" };

    /// <summary>Foreground (text) colors paired with <see cref="BackdropBackgroundColors"/>.</summary>
    public static readonly string[] BackdropForegroundColors =
        { "#AAFFFFFF", "#FFFFA500", "#FF44AAFF", "#FF66FF66", "#FFFF6666", "#FFFFFF66" };

    /// <summary>Foreground colors for standalone labels.</summary>
    public static readonly string[] LabelForegroundColors =
        { "#FFFFA500", "#FFFFFFFF", "#FF44AAFF", "#FFFF6666", "#FF66FF66", "#FFFFFF66" };

    // ───────── Paths ─────────

    /// <summary>
    /// Returns the application config directory path
    /// (e.g. ~/.config/CGReferenceBoard on Linux, %AppData%/CGReferenceBoard on Windows).
    /// </summary>
    public static string ConfigDirectory =>
        System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            ConfigDirName);
}
