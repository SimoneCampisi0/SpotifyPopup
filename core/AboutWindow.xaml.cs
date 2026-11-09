using System.Windows;
using System.Windows.Media.Imaging;

namespace SpotifyPopup;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        // WPF's default ICO source selects a small frame; use the original high-resolution artwork.
        using var logo = Application.GetResourceStream(new Uri("/SpotifyPopup;component/Assets/logo.ico", UriKind.Relative)).Stream;
        var icon = new IconBitmapDecoder(logo, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        AppLogo.Source = icon.Frames.OrderByDescending(frame => frame.PixelWidth).First();
        var assembly = typeof(AboutWindow).Assembly;
        VersionText.Text = "v" + assembly.GetName().Version?.ToString(3);
        using var stream = assembly.GetManifestResourceStream("SpotifyPopup.LICENSE")
            ?? throw new InvalidOperationException("The embedded license is missing.");
        using var reader = new StreamReader(stream);
        LicenseText.Text = reader.ReadToEnd();
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
