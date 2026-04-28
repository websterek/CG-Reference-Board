using System.ComponentModel;

namespace CGReferenceBoard.Services.Abstractions;

/// <summary>
/// Abstracts Avalonia Window chrome properties so ViewModels can
/// control always-on-top, transparency, opacity, and decorations without
/// taking a direct dependency on the Window instance.
/// </summary>
public interface IWindowChromeService : INotifyPropertyChanged
{
    bool IsAlwaysOnTop { get; set; }
    double Opacity { get; set; }
    bool ShowDecorations { get; set; }
}
