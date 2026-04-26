using CGReferenceBoard.Services.Transform;
using Xunit;

namespace CGReferenceBoard.Tests.Services.Transform;

public sealed class TransformCapabilitiesTests
{
    [Theory]
    [InlineData(true, TransformOperation.Move, true)]
    [InlineData(true, TransformOperation.Resize, false)]
    [InlineData(false, TransformOperation.Move, false)]
    [InlineData(false, TransformOperation.Resize, false)]
    public void AllowsOperation_UsesCapabilityFlags(bool canMove, TransformOperation operation, bool expected)
    {
        var capabilities = new TransformCapabilities(canMove, false, false, false);

        var allowed = capabilities.AllowsOperation(operation);

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void AllowsOperation_UsesResizeCapabilityFlag()
    {
        var capabilities = new TransformCapabilities(false, true, false, false);

        Assert.True(capabilities.AllowsOperation(TransformOperation.Resize));
        Assert.False(capabilities.AllowsOperation(TransformOperation.Move));
    }
}
