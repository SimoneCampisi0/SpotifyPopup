using System.Windows;

namespace SpotifyPopup;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var assembly = typeof(AboutWindow).Assembly;
        VersionText.Text = "v" + assembly.GetName().Version?.ToString(3);
        using var stream = assembly.GetManifestResourceStream("SpotifyPopup.LICENSE")
            ?? throw new InvalidOperationException("The embedded license is missing.");
        using var reader = new StreamReader(stream);
        LicenseText.Text = reader.ReadToEnd();
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
