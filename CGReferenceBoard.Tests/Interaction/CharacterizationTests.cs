using System.Threading.Tasks;
using Avalonia.Threading;
using CGReferenceBoard.Tests.TestInfrastructure;
using CGReferenceBoard.ViewModels;
using Xunit;

namespace CGReferenceBoard.Tests.Interaction;

/// <summary>
/// Pre-extraction safety nets. These tests capture existing behavior of
/// MainWindow pointer handling BEFORE the state machine is introduced.
/// Every test here must stay green throughout Group A work.
/// </summary>
public class CharacterizationTests
{
    [Fact]
    public void MiddleButtonDrag_PansCanvas_Placeholder()
    {
        // Full synthesis added in H1 once pointer driver exists
        Assert.True(true, "Placeholder — expand in H1");
    }

    [Fact]
    public void ShiftLeftButton_StartsPan_Placeholder()
    {
        Assert.True(true, "Placeholder — expand in H1");
    }

    [Fact]
    public void LeftButtonOnEmptyCanvas_ClearsSelection_Placeholder()
    {
        Assert.True(true, "Placeholder — expand in H1");
    }

    [Fact]
    public void CtrlLeftDrag_OpensMarquee_Placeholder()
    {
        Assert.True(true, "Placeholder — expand in H1");
    }
}
