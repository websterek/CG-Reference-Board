using System.Collections.Generic;
using System.Threading.Tasks;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Abstracts board file serialisation and I/O operations.
/// This is a stub interface for Step 6; the concrete implementation will be
/// introduced in Step 7 as a thin wrapper around <see cref="CGReferenceBoard.Services.BoardSerializer"/>.
/// </summary>
public interface IBoardService
{
    /// <summary>Serialises <paramref name="cells"/> and <paramref name="annotations"/> and writes them to <paramref name="filePath"/>.</summary>
    Task SaveAsync(
        string filePath,
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations);

    /// <summary>Reads <paramref name="filePath"/> and deserialises its contents into cell and annotation lists.</summary>
    Task<(List<CellViewModel> Cells, List<AnnotationViewModel> Annotations)> LoadAsync(string filePath);

    /// <summary>Serialises <paramref name="cells"/> and <paramref name="annotations"/> to a JSON string.</summary>
    string Serialize(
        IEnumerable<CellViewModel> cells,
        IEnumerable<AnnotationViewModel> annotations,
        string? basePath = null);

    /// <summary>Deserialises a JSON string produced by <see cref="Serialize"/> back into lists.</summary>
    (List<CellViewModel> Cells, List<AnnotationViewModel> Annotations) Deserialize(
        string json,
        string? basePath = null);
}
