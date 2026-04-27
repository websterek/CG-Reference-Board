using System.Collections.Generic;
using System.Threading.Tasks;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;

namespace CGReferenceBoard.Services;

public class BoardService : IBoardService
{
    public string Serialize(IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations, string? basePath = null)
        => BoardSerializer.Serialize(cells, annotations, basePath);

    public (List<CellViewModel> Cells, List<AnnotationViewModel> Annotations) Deserialize(string json, string? basePath = null)
        => BoardSerializer.Deserialize(json, basePath);

    public Task SaveAsync(string filePath, IEnumerable<CellViewModel> cells, IEnumerable<AnnotationViewModel> annotations)
        => BoardSerializer.SaveAsync(filePath, cells, annotations);

    public Task<(List<CellViewModel> Cells, List<AnnotationViewModel> Annotations)> LoadAsync(string filePath)
        => BoardSerializer.LoadAsync(filePath);
}
