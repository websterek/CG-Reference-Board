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
/// </summary>
public static class BoardSerializer
{
    /// <summary>
    /// Serializes the current board state to a JSON string.
    /// </summary>
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

    /// <summary>
    /// Serializes the current board state to a JSON string.
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
                c.BackgroundColor,
                c.ForegroundColor,
                FontSize = c.FontSize != 14 ? c.FontSize : (double?)null,
                ImageStretch = c.ImageStretch != "Uniform" ? c.ImageStretch : null
            }).ToList(),
            Annotations = annotations.Select(a => new
            {
                Type = a.Type != "Pencil" ? a.Type : null,
                a.Text,
                a.CanvasX,
                a.CanvasY,
                Color = a.Color != "#FFFF4444" ? a.Color : null,
                Thickness = a.Thickness != 2 ? a.Thickness : (double?)null,
                Points = a.Points.Select(p => new { p.X, p.Y }).ToList()
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(state, options);
    }

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

    private static CellViewModel DeserializeCell(JsonElement element, string? basePath)
    {
        double cx = element.GetProperty("CanvasX").GetDouble();
        double cy = element.GetProperty("CanvasY").GetDouble();
        int type = element.GetProperty("Type").GetInt32();
        int colSpan = element.TryGetProperty("ColSpan", out var col) ? col.GetInt32() : 1;
        int rowSpan = element.TryGetProperty("RowSpan", out var row) ? row.GetInt32() : 1;

        var cell = new CellViewModel { CanvasX = cx, CanvasY = cy, ColSpan = colSpan, RowSpan = rowSpan };

        switch ((CellType)type)
        {
            case CellType.Image:
                cell.SetImage(GetAbsolutePath(element.GetProperty("FilePath").GetString(), basePath)!);
                break;

            case CellType.Text:
            case CellType.Label:
            case CellType.Backdrop:
                cell.SetText(element.GetProperty("TextContent").GetString()!);
                cell.Type = (CellType)type;
                break;

            case CellType.Video:
                cell.SetVideo(
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

        return cell;
    }

    private static AnnotationViewModel DeserializeAnnotation(JsonElement element)
    {
        var annotation = new AnnotationViewModel
        {
            Type = element.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() ?? "Pencil" : "Pencil",
            Text = element.TryGetProperty("Text", out var textProp) ? textProp.GetString() ?? "" : "",
            CanvasX = element.GetProperty("CanvasX").GetDouble(),
            CanvasY = element.GetProperty("CanvasY").GetDouble(),
            Color = element.TryGetProperty("Color", out var colProp) ? colProp.GetString() ?? "#FFFF4444" : "#FFFF4444",
            Thickness = element.TryGetProperty("Thickness", out var thickProp) ? thickProp.GetDouble() : 2.0
        };

        if (element.TryGetProperty("Points", out var pts))
        {
            foreach (var pt in pts.EnumerateArray())
            {
                annotation.Points.Add(new Point(pt.GetProperty("X").GetDouble(), pt.GetProperty("Y").GetDouble()));
            }
        }

        return annotation;
    }
}
