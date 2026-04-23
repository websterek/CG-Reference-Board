using CommunityToolkit.Mvvm.ComponentModel;

namespace CGReferenceBoard.ViewModels;

/// <summary>
/// Base class for all ViewModels. Delegates INotifyPropertyChanged infrastructure
/// to <see cref="ObservableObject"/> from CommunityToolkit.Mvvm.
///
/// Compatibility note: <see cref="ObservableObject"/> exposes the same
/// <c>SetProperty</c> and <c>OnPropertyChanged</c> signatures used by
/// <see cref="CellViewModel"/> and <see cref="AnnotationViewModel"/>, so those
/// classes continue to compile without modification.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
