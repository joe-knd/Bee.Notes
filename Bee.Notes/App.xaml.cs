using CommunityToolkit.Mvvm.ComponentModel;
using FireFenyx.Wpf.Notifications.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using WpfNotes.Core.Services;
using WpfNotes.Features.Chat;
using WpfNotes.Features.DocumentHost;
using WpfNotes.Features.Editor;
using WpfNotes.Features.Home;
using WpfNotes.Navigation;

namespace WpfNotes;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();
        splash.Show();

        var services = new ServiceCollection();

        // Core
        services.AddSingleton<NavigationStore>();
        services.AddSingleton<INotesService, NotesService>();

        // Document host (tabs)
        services.AddSingleton<DocumentHostViewModel>();

        // Chat
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<ChatPersistenceService>();
        services.AddSingleton<ChatViewModel>();

        services.AddNotificationServices();


        // Factory for ViewModels with optional parameter
        services.AddSingleton<Func<Type, object?, ObservableObject>>(provider => (type, parameter) =>
        {
            if (type == typeof(EditorViewModel))
            {
                return parameter is not null
                    ? ActivatorUtilities.CreateInstance<EditorViewModel>(provider, parameter)
                    : ActivatorUtilities.CreateInstance<EditorViewModel>(provider);
            }

            return (ObservableObject)provider.GetRequiredService(type);
        });

        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddTransient<HomeViewModel>();
        services.AddTransient<EditorViewModel>();

        // MainWindow
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // Initialize navigation
        var store = Services.GetRequiredService<NavigationStore>();
        store.CurrentViewModel = Services.GetRequiredService<HomeViewModel>();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        splash.Close();
    }

}

