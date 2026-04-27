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
    public void Deserialize_PreservesCell_Fields()
    {
        var svc = CreateService();
        var cell = new CellViewModel
        {
            CanvasX = 120.0,
            CanvasY = 240.0,
            ColSpan = 3,
            RowSpan = 2,
            TextContent = "Hello round-trip",
            BackgroundColor = "#FF123456",
            ForegroundColor = "#FFABCDEF",
            FontSize = 32.0,
            PlaceholderColor = "#FF445566",
        };
        cell.Type = CGReferenceBoard.Models.CellType.Label;

        var json = svc.Serialize(new List<CellViewModel> { cell }, new List<AnnotationViewModel>());
        var (cells, _) = svc.Deserialize(json);

        Assert.Single(cells);
        var c = cells[0];
        Assert.Equal(cell.CanvasX, c.CanvasX);
        Assert.Equal(cell.CanvasY, c.CanvasY);
        Assert.Equal(cell.ColSpan, c.ColSpan);
        Assert.Equal(cell.RowSpan, c.RowSpan);
        Assert.Equal(cell.TextContent, c.TextContent);
        Assert.Equal(cell.BackgroundColor, c.BackgroundColor);
        Assert.Equal(cell.ForegroundColor, c.ForegroundColor);
        Assert.Equal(cell.FontSize, c.FontSize);
        Assert.Equal(cell.PlaceholderColor, c.PlaceholderColor);
        Assert.Equal(cell.Type, c.Type);
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
