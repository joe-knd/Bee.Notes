using System.ComponentModel;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using WpfNotes.Core.Services;
using WpfNotes.Features.Chat;
using WpfNotes.Features.DocumentHost;
using WpfNotes.Features.Editor;
using WpfNotes.Features.Home;
using WpfNotes.Navigation;

namespace WpfNotes;

public partial class MainWindow : Window
{
    private readonly NavigationStore _navigationStore;
    private readonly INavigationService _navigation;
    private readonly IChatService _chatService;

    public DocumentHostViewModel DocumentHost { get; }

    public MainWindow(NavigationStore navigationStore, DocumentHostViewModel host, INavigationService navigation, IChatService chatService)
    {
        DocumentHost = host;
        _chatService = chatService;
        InitializeComponent();
        DataContext = navigationStore;
        _navigationStore = navigationStore;
        _navigation = navigation;
    }

    private void HomeTab_Click(object sender, RoutedEventArgs e)
    {
        DocumentHost.ActiveDocument = null;
        _navigation.Navigate<HomeViewModel>();
    }

    private void ChatTab_Click(object sender, RoutedEventArgs e)
    {
        DocumentHost.ActiveDocument = null;
        _navigation.Navigate<ChatViewModel>();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        await _chatService.DisconnectAsync();
        base.OnClosing(e);
    }

    private void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is EditorViewModel)
            _navigation.Navigate<DocumentHostViewModel>();
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        DocumentHost.Open(null);
        _navigation.Navigate<DocumentHostViewModel>();
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Open File"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await DocumentHost.OpenFile(dialog.FileName);
            _navigation.Navigate<DocumentHostViewModel>();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void About_Click(object sender, RoutedEventArgs e)
        => new AboutWindow { Owner = this }.ShowDialog();
}