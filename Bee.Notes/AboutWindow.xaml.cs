using System.Reflection;
using System.Windows;

namespace WpfNotes;

/// <summary>
/// About dialog showing the application name, logo, and version.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is not null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Version 1.0.0";
    }

    private void OK_Click(object sender, RoutedEventArgs e) => Close();
}
