namespace SpotifyPopup;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Set the process mode before WPF or the notification icon creates any HWNDs.
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        var application = new App();
        application.InitializeComponent();
        return application.Run();
    }
}
