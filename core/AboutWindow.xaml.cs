using System.Windows;
using System.Windows.Media.Imaging;

namespace SpotifyPopup;

public partial class AboutWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    public AboutWindow()
    {
        InitializeComponent();
        Closed += (_, _) => lifetime.Cancel();
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
    private async void CheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false; DownloadButton.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = Localizer.Get(TextKeys.CheckingUpdates);
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            cancellation.CancelAfter(TimeSpan.FromSeconds(10));
            var latest = await new UpdateService(client).LatestAsync(cancellation.Token);
            if (lifetime.IsCancellationRequested) return;
            var installed = typeof(AboutWindow).Assembly.GetName().Version!;
            var current = new Version(installed.Major, installed.Minor, installed.Build);
            var available = latest > current;
            UpdateStatus.Text = available ? string.Format(Localizer.Get(TextKeys.UpdateAvailable), latest) : Localizer.Get(TextKeys.UpToDate);
            DownloadButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Diagnostics.Record("update-check", ex); if (!lifetime.IsCancellationRequested) UpdateStatus.Text = Localizer.Get(TextKeys.UpdateFailed); }
        finally { if (!lifetime.IsCancellationRequested) UpdateButton.IsEnabled = true; }
    }
    private void DownloadClick(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateService.DownloadUrl) { UseShellExecute = true }); }
        catch (Exception ex) { Diagnostics.Record("update-open", ex); UpdateStatus.Text = Localizer.Get(TextKeys.UpdateFailed); }
    }
    private void CopyDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Diagnostics.Report()); UpdateStatus.Text = Localizer.Get(TextKeys.DiagnosticsCopied); }
        catch (Exception ex) { Diagnostics.Record("diagnostics-copy", ex); UpdateStatus.Text = Localizer.Get(TextKeys.DiagnosticsFailed); }
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
