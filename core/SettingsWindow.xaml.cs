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
    public string SelectedLanguage => Localizer.Normalize(LanguageBox.SelectedValue as string);
    private string errorKey = "";
    public SettingsWindow(AppSettings settings, Action<double> preview, Func<string, string, string?> validate)
    {
        this.preview = preview; this.validate = validate;
        InitializeComponent();
        LanguageBox.SelectedValue = settings.Language;
        Localizer.Current.PropertyChanged += LanguageChanged;
        Closed += (_, _) => Localizer.Current.PropertyChanged -= LanguageChanged;
        ShowAlbumBox.IsChecked = settings.ShowAlbum; SnapBox.IsChecked = settings.SnapToEdges;
        OpacitySlider.Value = settings.LockedOpacity * 100; UpdateOpacity();
        ToggleBox.Text = settings.ToggleShortcut; UnlockBox.Text = settings.UnlockShortcut;
        ready = true;
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        UpdateOpacity(); preview(LockedOpacity);
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var error = validate(ToggleShortcut, UnlockShortcut);
        errorKey = error ?? ""; ErrorText.Text = Localizer.Get(errorKey);
        if (error != null) return;
        DialogResult = true;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void AboutClick(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void LanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ErrorText.Text = Localizer.Get(errorKey);
    private void UpdateOpacity() => OpacityValue.Text = OpacitySlider.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
