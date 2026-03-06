using CommunityToolkit.Mvvm.ComponentModel;
using WpfNotes.Navigation;

namespace WpfNotes.Core.Services;

/// <summary>
/// Default <see cref="INavigationService"/> implementation that resolves view-models via a factory delegate.
/// </summary>
/// <param name="store">The navigation store that holds the current view-model.</param>
/// <param name="factory">Factory delegate used to create view-model instances.</param>
public class NavigationService(NavigationStore store, Func<Type, object?, ObservableObject> factory) : INavigationService
{
    /// <inheritdoc />
    public void Navigate<TViewModel>() where TViewModel : ObservableObject =>
        store.CurrentViewModel = factory(typeof(TViewModel), null);

    /// <inheritdoc />
    public void Navigate<TViewModel>(object? parameter) where TViewModel : ObservableObject =>
        store.CurrentViewModel = factory(typeof(TViewModel), parameter);
}