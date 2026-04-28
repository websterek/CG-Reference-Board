using System.ComponentModel;
using CGReferenceBoard.Services.Abstractions;

namespace CGReferenceBoard.Services;

internal sealed class NullWindowChromeService : IWindowChromeService
{
#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    public bool IsAlwaysOnTop { get; set; }
    public double Opacity { get; set; } = 1.0;
    public bool ShowDecorations { get; set; } = true;
}
