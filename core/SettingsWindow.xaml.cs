using System.Windows;

namespace SpotifyPopup;

public partial class SettingsWindow : Window
{
    private readonly Action<double> preview;
    private readonly Func<string, string, string?> validate;
    private bool ready;
    public bool ShowAlbum => ShowAlbumBox.IsChecked == true;
    public bool SnapToEdges => SnapBox.IsChecked == true;
    public double LockedOpacity => OpacitySlider.Value / 100;
    public string ToggleShortcut => ToggleBox.Text.Trim();
    public string UnlockShortcut => UnlockBox.Text.Trim();
    public SettingsWindow(AppSettings settings, Action<double> preview, Func<string, string, string?> validate)
    {
        this.preview = preview; this.validate = validate;
        InitializeComponent();
        ShowAlbumBox.IsChecked = settings.ShowAlbum; SnapBox.IsChecked = settings.SnapToEdges;
        OpacitySlider.Value = settings.LockedOpacity * 100; OpacityValue.Text = $"{OpacitySlider.Value:0}%";
        ToggleBox.Text = settings.ToggleShortcut; UnlockBox.Text = settings.UnlockShortcut;
        ready = true;
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        OpacityValue.Text = $"{OpacitySlider.Value:0}%"; preview(LockedOpacity);
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var error = validate(ToggleShortcut, UnlockShortcut);
        if (error != null) { ErrorText.Text = error; return; }
        DialogResult = true;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
