using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfNotes.Core.Services;

/// <summary>
/// Provides navigation between application views.
/// </summary>
public interface INavigationService
{
    /// <summary>Navigate to the specified view-model type.</summary>
    /// <typeparam name="TViewModel">The target view-model type.</typeparam>
    void Navigate<TViewModel>() where TViewModel : ObservableObject;

    /// <summary>Navigate to the specified view-model type with a parameter.</summary>
    /// <typeparam name="TViewModel">The target view-model type.</typeparam>
    /// <param name="parameter">Optional parameter passed to the view-model.</param>
    void Navigate<TViewModel>(object? parameter) where TViewModel : ObservableObject;
}