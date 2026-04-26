using Avalonia;
using CGReferenceBoard.Services.Transform;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformItemSnapshotTests
{
    [Fact]
    public void TransformItemSnapshot_HasNoPublicConstructors()
    {
        var constructors = typeof(TransformItemSnapshot).GetConstructors();

        Assert.Empty(constructors);
    }

    [Fact]
    public void FromCell_CreatesCellSnapshot()
    {
        var cell = new CellViewModel
        {
            CanvasX = 10,
            CanvasY = 20,
            ColSpan = 3,
            RowSpan = 4,
        };
        var bounds = new Rect(10, 20, 30, 40);

        var snapshot = TransformItemSnapshot.FromCell(cell, bounds);

        Assert.Same(cell, snapshot.Cell);
        Assert.Null(snapshot.Annotation);
        Assert.Equal(bounds, snapshot.Bounds);
        Assert.Equal(10, snapshot.CanvasX);
        Assert.Equal(20, snapshot.CanvasY);
        Assert.Equal(3, snapshot.ColSpan);
        Assert.Equal(4, snapshot.RowSpan);
    }

    [Fact]
    public void FromAnnotation_CreatesAnnotationSnapshot()
    {
        var annotation = new AnnotationViewModel
        {
            CanvasX = 15,
            CanvasY = 25,
        };
        var bounds = new Rect(15, 25, 35, 45);

        var snapshot = TransformItemSnapshot.FromAnnotation(annotation, bounds);

        Assert.Null(snapshot.Cell);
        Assert.Same(annotation, snapshot.Annotation);
        Assert.Equal(bounds, snapshot.Bounds);
        Assert.Equal(15, snapshot.CanvasX);
        Assert.Equal(25, snapshot.CanvasY);
        Assert.Equal(0, snapshot.ColSpan);
        Assert.Equal(0, snapshot.RowSpan);
    }
}
