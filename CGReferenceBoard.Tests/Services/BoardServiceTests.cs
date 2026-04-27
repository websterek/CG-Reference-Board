using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CGReferenceBoard.Services;
using CGReferenceBoard.Services.Abstractions;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services;

public class BoardServiceTests
{
    private IBoardService CreateService() => new BoardService();

    [Fact]
    public void Serialize_ReturnsNonEmptyJson()
    {
        var svc = CreateService();
        var json = svc.Serialize(new List<CellViewModel>(), new List<AnnotationViewModel>());
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void Deserialize_RoundTrips_Serialize()
    {
        var svc = CreateService();
        var json = svc.Serialize(new List<CellViewModel>(), new List<AnnotationViewModel>());
        var (cells, annotations) = svc.Deserialize(json);
        Assert.NotNull(cells);
        Assert.NotNull(annotations);
    }

    [Fact]
    public async Task SaveAsync_WritesFile_And_LoadAsync_ReadsIt()
    {
        var svc = CreateService();
        var path = Path.GetTempFileName();
        try
        {
            await svc.SaveAsync(path, new List<CellViewModel>(), new List<AnnotationViewModel>());
            var (cells, annotations) = await svc.LoadAsync(path);
            Assert.NotNull(cells);
            Assert.NotNull(annotations);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
