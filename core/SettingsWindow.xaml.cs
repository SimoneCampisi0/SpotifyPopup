using System.Windows;

namespace SpotifyPopup;

public partial class SettingsWindow : Window
{
    private readonly Action<double> preview;
    private readonly Action? resetPlacement;
    private readonly AppSettings original;
    private readonly Func<string, string, string?> validate;
    private bool ready;
    public bool ShowAlbum => ShowAlbumBox.IsChecked == true;
    public bool SnapToEdges => SnapBox.IsChecked == true;
    public double LockedOpacity => OpacitySlider.Value / 100;
    public string ToggleShortcut => ToggleBox.Text.Trim();
    public string UnlockShortcut => UnlockBox.Text.Trim();
    public string SelectedLanguage => Localizer.Normalize(LanguageBox.SelectedValue as string);
    public bool ResetRequested { get; private set; }
    private string errorKey = "";
    public SettingsWindow(AppSettings settings, Action<double> preview, Func<string, string, string?> validate, Action? resetPlacement = null)
    {
        this.preview = preview; this.validate = validate; this.resetPlacement = resetPlacement; original = settings.Clone();
        InitializeComponent();
        Fill(settings);
        Localizer.Current.PropertyChanged += LanguageChanged;
        Closed += (_, _) => Localizer.Current.PropertyChanged -= LanguageChanged;
        ready = true;
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        UpdateOpacity(); preview(LockedOpacity);
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (!AppSettings.ValidColor(ColorBox.Text.Trim())) { SetError(TextKeys.InvalidColor); return; }
        if (!int.TryParse(DelayBox.Text, out var delay) || delay < 10 || delay > 3600) { SetError(TextKeys.InvalidDelay); return; }
        var error = validate(ToggleShortcut, UnlockShortcut);
        SetError(error ?? "");
        if (error != null) return;
        DialogResult = true;
    }
    public AppSettings Draft
    {
        get
        {
            var value = original.Clone();
            value.ShowAlbum = ShowAlbum; value.SnapToEdges = SnapToEdges; value.LockedOpacity = LockedOpacity;
            value.ToggleShortcut = ToggleShortcut; value.UnlockShortcut = UnlockShortcut; value.Language = SelectedLanguage;
            value.StartWithWindows = StartupBox.IsChecked == true; value.StartInTray = TrayBox.IsChecked == true;
            value.BackgroundMode = BackgroundBox.SelectedValue as string ?? "artwork"; value.BackgroundColor = ColorBox.Text.Trim();
            value.AnimateBackground = AnimationBox.IsChecked == true; value.HideWhenDisconnected = DisconnectedBox.IsChecked == true;
            value.HideWhenPaused = PausedBox.IsChecked == true; value.PauseDelaySeconds = int.TryParse(DelayBox.Text, out var delay) ? delay : 60;
            value.PlayShortcut = PlayBox.Text.Trim(); value.PreviousShortcut = PreviousBox.Text.Trim(); value.NextShortcut = NextBox.Text.Trim();
            value.BackwardShortcut = BackwardBox.Text.Trim(); value.ForwardShortcut = ForwardBox.Text.Trim();
            return value;
        }
    }
    private void Fill(AppSettings value)
    {
        LanguageBox.SelectedValue = value.Language; ShowAlbumBox.IsChecked = value.ShowAlbum; SnapBox.IsChecked = value.SnapToEdges;
        OpacitySlider.Value = value.LockedOpacity * 100; UpdateOpacity(); ToggleBox.Text = value.ToggleShortcut; UnlockBox.Text = value.UnlockShortcut;
        StartupBox.IsChecked = value.StartWithWindows; TrayBox.IsChecked = value.StartInTray;
        BackgroundBox.SelectedValue = value.BackgroundMode; ColorBox.Text = value.BackgroundColor; AnimationBox.IsChecked = value.AnimateBackground;
        DisconnectedBox.IsChecked = value.HideWhenDisconnected; PausedBox.IsChecked = value.HideWhenPaused;
        DelayBox.Text = value.PauseDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PlayBox.Text = value.PlayShortcut; PreviousBox.Text = value.PreviousShortcut; NextBox.Text = value.NextShortcut;
        BackwardBox.Text = value.BackwardShortcut; ForwardBox.Text = value.ForwardShortcut;
    }
    private void SetError(string key)
    {
        errorKey = key; ErrorText.Text = Localizer.Get(key); ErrorText.BringIntoView();
    }
    private void ResetPlacementClick(object sender, RoutedEventArgs e)
    {
        resetPlacement?.Invoke();
        original.Normal = new(); original.Compact = new() { Width = 460, Height = 142 }; original.Essential = new() { Width = 330, Height = 112 };
    }
    private void ResetAllClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Localizer.Get(TextKeys.ResetConfirm), Localizer.Get(TextKeys.ResetAll), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        ResetRequested = true; var defaults = new AppSettings(); Fill(defaults);
        original.Normal = defaults.Normal; original.Compact = defaults.Compact; original.Essential = defaults.Essential;
        original.CompactMode = false; original.EssentialMode = false;
        preview(defaults.LockedOpacity);
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void AboutClick(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void LanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ErrorText.Text = Localizer.Get(errorKey);
    private void UpdateOpacity() => OpacityValue.Text = OpacitySlider.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
