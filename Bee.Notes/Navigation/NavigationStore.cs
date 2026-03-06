using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfNotes.Navigation;

/// <summary>
/// Observable store that holds the currently active view-model for main content navigation.
/// </summary>
public class NavigationStore : ObservableObject
{
    private ObservableObject? _currentViewModel;

    /// <summary>The view-model currently displayed in the main content area.</summary>
    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }
}
