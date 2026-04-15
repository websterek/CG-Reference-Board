using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using CGReferenceBoard.Models;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

/// <summary>
/// Handles serialization and deserialization of board state (cells and annotations).
///
/// File-size optimisations applied:
///   • Annotation points are stored as compact [x, y] arrays rounded to 1 decimal
///     place instead of {"X":…,"Y":…} objects with 16-digit precision (~59 % smaller).
///   • Default BackgroundColor / ForegroundColor values are omitted (null-when-default).
///   • All other properties that match their ViewModel defaults are also omitted.
///   • Backward-compatible: the deserializer accepts both the old {"X","Y"} object
///     format and the new [x, y] array format for annotation points.
/// </summary>
public static class BoardSerializer
{
    // ───────── Default value constants (must match CellViewModel field initialisers) ─────────
    private const string DefaultBackgroundColor = "#885A3A10";
    private const string DefaultForegroundColor = "#FFFFA500";
    private const string DefaultPlaceholderColor = "#FF2A2A2A";
    private const string DefaultImageStretch = "UniformToFill";
    private const double DefaultFontSize = 48.0;
    private const string DefaultAnnotationColor = "#FFFF4444";
    private const double DefaultAnnotationThickness = 2.0;

    // ───────── Path helpers ─────────

    private static string? GetRelativePath(string? fullPath, string? basePath)
    {
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(basePath))
            return fullPath;
        try
        {
            string baseDir = Path.GetDirectoryName(basePath) ?? basePath;
            return Path.GetRelativePath(baseDir, fullPath);
        }
        catch
        {
            return fullPath;
        }
    }

    private static string? GetAbsolutePath(string? relPath, string? basePath)
    {
        if (string.IsNullOrEmpty(relPath) || string.IsNullOrEmpty(basePath))
            return relPath;
        if (Path.IsPathRooted(relPath))
            return relPath;
        try
        {
            string baseDir = Path.GetDirectoryName(basePath) ?? basePath;
            return Path.GetFullPath(Path.Combine(baseDir, relPath));
        }
        catch
        {
            return relPath;
        }
    }

    // ───────── Serialization ─────────

    /// <summary>
    /// Serializes the current board state to a compact JSON string.
    /// </summary>
    public static string Serialize(IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations, string? basePath = null)
    {
        var state = new
        {
            Cells = cells.Where(c => c.Type != CellType.None).Select(c => new
            {
                c.CanvasX,
                c.CanvasY,
                ColSpan = c.ColSpan > 1 ? c.ColSpan : (int?)null,
                RowSpan = c.RowSpan > 1 ? c.RowSpan : (int?)null,
                c.Type,
                FilePath = GetRelativePath(c.FilePath, basePath),
                VideoPath = GetRelativePath(c.VideoPath, basePath),
                c.TextContent,
                BackgroundColor = c.BackgroundColor != DefaultBackgroundColor ? c.BackgroundColor : null,
                ForegroundColor = c.ForegroundColor != DefaultForegroundColor ? c.ForegroundColor : null,
                FontSize = c.FontSize != DefaultFontSize ? c.FontSize : (double?)null,
                ImageStretch = c.ImageStretch != DefaultImageStretch ? c.ImageStretch : null,
                PlaceholderColor = c.PlaceholderColor != DefaultPlaceholderColor ? c.PlaceholderColor : null
            }).ToList(),
            Annotations = annotations.Select(a => new
            {
                Type = a.Type != "Brush" ? a.Type : null,
                Text = !string.IsNullOrEmpty(a.Text) ? a.Text : null,
                a.CanvasX,
                a.CanvasY,
                Color = a.Color != DefaultAnnotationColor ? a.Color : null,
                Thickness = a.Thickness != DefaultAnnotationThickness ? a.Thickness : (double?)null,
                // Compact format: each point is a [x, y] array rounded to 1 decimal place.
                Points = a.Points.Select(p => new[] { Math.Round(p.X, 1), Math.Round(p.Y, 1) }).ToList()
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(state, options);
    }

    // ───────── Deserialization ─────────

    /// <summary>
    /// Deserializes board state from a JSON string.
    /// Supports both the legacy format (top-level array of cells) and
    /// the current format ({ Cells: [...], Annotations: [...] }).
    /// </summary>
    public static (List<CellViewModel> Cells, List<AnnotationViewModel> Annotations) Deserialize(string json, string? basePath = null)
    {
        var cells = new List<CellViewModel>();
        var annotations = new List<AnnotationViewModel>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement cellsElement = default;
        JsonElement annotationsElement = default;

        // Support legacy format (top-level array) and current format (object)
        if (root.ValueKind == JsonValueKind.Array)
        {
            cellsElement = root;
        }
        else
        {
            if (root.TryGetProperty("Cells", out var c))
                cellsElement = c;
            if (root.TryGetProperty("Annotations", out var a))
                annotationsElement = a;
        }

        if (cellsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in cellsElement.EnumerateArray())
            {
                cells.Add(DeserializeCell(element, basePath));
            }
        }

        if (annotationsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in annotationsElement.EnumerateArray())
            {
                annotations.Add(DeserializeAnnotation(element));
            }
        }

        return (cells, annotations);
    }

    // ───────── Async file helpers ─────────

    /// <summary>
    /// Saves the board state to a file asynchronously.
    /// </summary>
    public static async Task SaveAsync(string filePath, IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations)
    {
        string json = Serialize(cells, annotations, filePath);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads the board state from a file asynchronously.
    /// </summary>
    public static async Task<(List<CellViewModel> Cells, List<AnnotationViewModel> Annotations)> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return (new List<CellViewModel>(), new List<AnnotationViewModel>());

        string json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json, filePath);
    }

    // ───────── Cell deserialization ─────────

    private static CellViewModel DeserializeCell(JsonElement element, string? basePath)
    {
        if (!element.TryGetProperty("CanvasX", out var cxProp) || cxProp.ValueKind != JsonValueKind.Number)
            return new CellViewModel();
        if (!element.TryGetProperty("CanvasY", out var cyProp) || cyProp.ValueKind != JsonValueKind.Number)
            return new CellViewModel();
        if (!element.TryGetProperty("Type", out var typeProp) || typeProp.ValueKind != JsonValueKind.Number)
            return new CellViewModel();

        double cx = cxProp.GetDouble();
        double cy = cyProp.GetDouble();
        int type = typeProp.GetInt32();
        int colSpan = element.TryGetProperty("ColSpan", out var col) ? col.GetInt32() : 1;
        int rowSpan = element.TryGetProperty("RowSpan", out var row) ? row.GetInt32() : 1;

        var cell = new CellViewModel { CanvasX = cx, CanvasY = cy, ColSpan = colSpan, RowSpan = rowSpan };

        switch ((CellType)type)
        {
            case CellType.Image:
                cell.SetImageDeferred(GetAbsolutePath(element.GetProperty("FilePath").GetString(), basePath)!);
                break;

            case CellType.Text:
            case CellType.Label:
            case CellType.Backdrop:
                cell.SetText(element.GetProperty("TextContent").GetString()!);
                cell.Type = (CellType)type;
                break;

            case CellType.Video:
                cell.SetVideoDeferred(
                    GetAbsolutePath(element.GetProperty("VideoPath").GetString(), basePath)!,
                    GetAbsolutePath(element.GetProperty("FilePath").GetString(), basePath)!);
                break;
        }

        if (element.TryGetProperty("BackgroundColor", out var bg) && bg.ValueKind == JsonValueKind.String)
            cell.BackgroundColor = bg.GetString()!;
        if (element.TryGetProperty("ForegroundColor", out var fg) && fg.ValueKind == JsonValueKind.String)
            cell.ForegroundColor = fg.GetString()!;
        if (element.TryGetProperty("FontSize", out var fs) && fs.ValueKind == JsonValueKind.Number)
            cell.FontSize = fs.GetDouble();
        if (element.TryGetProperty("ImageStretch", out var stretch) && stretch.ValueKind == JsonValueKind.String)
            cell.ImageStretch = stretch.GetString()!;
        if (element.TryGetProperty("PlaceholderColor", out var pc) && pc.ValueKind == JsonValueKind.String)
            cell.PlaceholderColor = pc.GetString()!;

        return cell;
    }

    // ───────── Annotation deserialization ─────────

    private static AnnotationViewModel DeserializeAnnotation(JsonElement element)
    {
        if (!element.TryGetProperty("CanvasX", out var cxProp) || cxProp.ValueKind != JsonValueKind.Number)
            return new AnnotationViewModel();
        if (!element.TryGetProperty("CanvasY", out var cyProp) || cyProp.ValueKind != JsonValueKind.Number)
            return new AnnotationViewModel();

        var annotation = new AnnotationViewModel
        {
            Type = MapLegacyAnnotationType(element.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "Brush" : "Brush"),
            Text = element.TryGetProperty("Text", out var textProp) ? textProp.GetString() ?? "" : "",
            CanvasX = cxProp.GetDouble(),
            CanvasY = cyProp.GetDouble(),
            Color = element.TryGetProperty("Color", out var colProp) ? colProp.GetString() ?? DefaultAnnotationColor : DefaultAnnotationColor,
            Thickness = element.TryGetProperty("Thickness", out var thickProp) ? thickProp.GetDouble() : DefaultAnnotationThickness
        };

        if (element.TryGetProperty("Points", out var pts))
        {
            foreach (var pt in pts.EnumerateArray())
            {
                // Backward-compatible: accept both the old {"X":…,"Y":…} object format
                // and the new compact [x, y] array format.
                if (pt.ValueKind == JsonValueKind.Array)
                {
                    // New compact format: [x, y]
                    var enumerator = pt.EnumerateArray();
                    enumerator.MoveNext();
                    double x = enumerator.Current.GetDouble();
                    enumerator.MoveNext();
                    double y = enumerator.Current.GetDouble();
                    annotation.Points.Add(new Point(x, y));
                }
                else
                {
                    // Legacy object format: {"X": …, "Y": …}
                    annotation.Points.Add(new Point(
                        pt.GetProperty("X").GetDouble(),
                        pt.GetProperty("Y").GetDouble()));
                }
            }
        }

        return annotation;
    }

    /// <summary>
    /// Maps legacy annotation type names to their current equivalents.
    /// "Pencil" was renamed to "Brush"; existing saved boards are migrated automatically.
    /// </summary>
    private static string MapLegacyAnnotationType(string type) =>
        type == "Pencil" ? "Brush" : type;
}
