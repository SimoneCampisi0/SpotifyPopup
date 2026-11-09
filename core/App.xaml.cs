using System.Windows;

namespace SpotifyPopup;

public partial class App : Application
{
    private SingleInstance? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            instance = new SingleInstance();
            if (!instance.Acquire()) { await instance.SignalAsync(); Shutdown(); return; }
            var window = new MainWindow(); MainWindow = window;
            _ = instance.ListenAsync(() => Dispatcher.BeginInvoke(new Action(window.RevealExisting)));
            window.Start();
        }
        catch (Exception ex) { Diagnostics.Record("startup", ex); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
