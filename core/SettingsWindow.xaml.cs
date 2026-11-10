using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
    public bool ResetPlacementRequested { get; private set; }
    public bool CheckUpdatesOnStartup => AutoUpdateBox.IsChecked == true;
    public bool AlwaysOnTop => TopmostBox.IsChecked == true;
    private string errorKey = "";
    public SettingsWindow(AppSettings settings, Action<double> preview, Func<string, string, string?> validate, Action? resetPlacement = null)
    {
        this.preview = preview; this.validate = validate; this.resetPlacement = resetPlacement; original = settings.Clone();
        InitializeComponent();
        Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.AccentColor));
        Fill(settings);
        Localizer.Current.PropertyChanged += LanguageChanged;
        Closed += (_, _) => Localizer.Current.PropertyChanged -= LanguageChanged;
        ready = true; SelectSection(0); UpdateAppearance();
        Loaded += (_, _) => NativeMethods.EnsureVisible(this);
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        UpdateOpacity(); preview(LockedOpacity);
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (!AppSettings.ValidColor(AccentBox.Text.Trim()))
        { SelectSection(1); SetError(TextKeys.InvalidColor); AccentBox.Focus(); return; }
        if (BackgroundBox.SelectedValue as string == "custom" && !AppSettings.ValidColor(ColorBox.Text.Trim()))
        { SelectSection(1); SetError(TextKeys.InvalidColor); ColorBox.Focus(); return; }
        if (PausedBox.IsChecked == true && (!int.TryParse(DelayBox.Text, out var delay) || delay < 10 || delay > 3600))
        { SelectSection(2); SetError(TextKeys.InvalidDelay); DelayBox.Focus(); return; }
        var error = validate(ToggleShortcut, UnlockShortcut);
        SetError(error ?? "");
        if (error != null) { SelectSection(error == TextKeys.StartupFailed ? 0 : 3); return; }
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
            value.AlwaysOnTop = AlwaysOnTop; value.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
            value.AccentColor = AccentBox.Text.Trim(); value.CoverSizePercent = (int)Math.Round(CoverSizeSlider.Value);
            value.ReduceMotion = ReduceMotionBox.IsChecked == true;
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
        TopmostBox.IsChecked = value.AlwaysOnTop; AutoUpdateBox.IsChecked = value.CheckUpdatesOnStartup;
        AccentBox.Text = value.AccentColor; CoverSizeSlider.Value = value.CoverSizePercent;
        ReduceMotionBox.IsChecked = value.ReduceMotion;
        BackgroundBox.SelectedValue = value.BackgroundMode; ColorBox.Text = value.BackgroundColor; AnimationBox.IsChecked = value.AnimateBackground;
        DisconnectedBox.IsChecked = value.HideWhenDisconnected; PausedBox.IsChecked = value.HideWhenPaused;
        DelayBox.Text = value.PauseDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        PlayBox.Text = value.PlayShortcut; PreviousBox.Text = value.PreviousShortcut; NextBox.Text = value.NextShortcut;
        BackwardBox.Text = value.BackwardShortcut; ForwardBox.Text = value.ForwardShortcut;
        UpdateAppearance();
    }
    private void SetError(string key)
    {
        errorKey = key; ErrorText.Text = Localizer.Get(key); ErrorText.BringIntoView();
    }
    private void ResetPlacementClick(object sender, RoutedEventArgs e)
    {
        // Queue this change in the draft. Cancel must not move or persist the main window.
        ResetPlacementRequested = true;
        SetError(TextKeys.DefaultsReady);
        original.Normal = new(); original.Compact = new() { Width = 460, Height = 166 }; original.Essential = new() { Width = 330, Height = 116 };
    }
    private void ResetAllClick(object sender, RoutedEventArgs e)
    {
        SaveButton.IsDefault = false; ConfirmResetButton.IsDefault = true;
        ResetConfirmation.Visibility = Visibility.Visible; ConfirmResetButton.Focus(); ResetConfirmation.BringIntoView();
    }
    private void CancelResetClick(object sender, RoutedEventArgs e)
    {
        ResetConfirmation.Visibility = Visibility.Collapsed; ConfirmResetButton.IsDefault = false; SaveButton.IsDefault = true; ResetAllButton.Focus();
    }
    private void DialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ResetConfirmation.Visibility == Visibility.Visible)
        { e.Handled = true; CancelResetClick(sender, e); }
    }
    private void ConfirmResetClick(object sender, RoutedEventArgs e)
    {
        ResetConfirmation.Visibility = Visibility.Collapsed; ConfirmResetButton.IsDefault = false; SaveButton.IsDefault = true;
        ResetRequested = true; var defaults = new AppSettings(); Fill(defaults);
        original.Normal = defaults.Normal; original.Compact = defaults.Compact; original.Essential = defaults.Essential;
        original.CompactMode = false; original.EssentialMode = false;
        preview(defaults.LockedOpacity); SetError(TextKeys.DefaultsReady); SaveButton.Focus();
    }
    internal void SelectSection(int index)
    {
        Navigation.SelectedIndex = Math.Clamp(index, 0, 4); UpdateSection();
    }
    private void NavigationChanged(object sender, SelectionChangedEventArgs e) { if (ready) UpdateSection(); }
    private void UpdateSection()
    {
        var panels = new[] { GeneralPanel, AppearancePanel, VisibilityPanel, ShortcutsPanel, RecoveryPanel };
        var index = Math.Clamp(Navigation.SelectedIndex, 0, 4);
        if (index != 4) { ResetConfirmation.Visibility = Visibility.Collapsed; ConfirmResetButton.IsDefault = false; SaveButton.IsDefault = true; }
        for (var i = 0; i < panels.Length; i++) panels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        var titles = new[] { TextKeys.GeneralSection, TextKeys.Appearance, TextKeys.VisibilityHeading, TextKeys.ShortcutsSection, TextKeys.RecoveryHeading };
        var descriptions = new[] { TextKeys.GeneralDescription, TextKeys.AppearanceDescription, TextKeys.VisibilityDescription, TextKeys.ShortcutsDescription, TextKeys.RecoveryDescription };
        SectionTitle.Text = Localizer.Get(titles[index]); SectionDescription.Text = Localizer.Get(descriptions[index]);
        SettingsScroll.ScrollToTop();
    }
    private void AppearanceChanged(object sender, SelectionChangedEventArgs e) { if (ready) UpdateAppearance(); }
    private void PauseOptionChanged(object sender, RoutedEventArgs e) { if (ready) UpdateAppearance(); }
    private void UpdateAppearance()
    {
        if (CustomColorPanel == null || DelayBox == null) return;
        CustomColorPanel.Visibility = BackgroundBox.SelectedValue as string == "custom" ? Visibility.Visible : Visibility.Collapsed;
        AnimationBox.IsEnabled = BackgroundBox.SelectedValue as string != "fixed";
        DelayBox.IsEnabled = PausedBox.IsChecked == true;
        UpdateColorSwatch();
    }
    private void ColorChanged(object sender, TextChangedEventArgs e) => UpdateColorSwatch();
    private void UpdateColorSwatch()
    {
        if (ColorSwatch != null && ColorBox != null && AppSettings.ValidColor(ColorBox.Text.Trim()))
            ColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ColorBox.Text.Trim()));
    }
    private void ShortcutCapture(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab || key == Key.Escape) return;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        e.Handled = true;
        var box = (TextBox)sender;
        if (key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None) { box.Text = ""; return; }
        if (Keyboard.Modifiers == ModifierKeys.None) return;
        try
        {
            var gesture = new KeyGesture(key, Keyboard.Modifiers);
            box.Text = new KeyGestureConverter().ConvertToInvariantString(gesture) ?? "";
            box.CaretIndex = box.Text.Length;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException) { }
    }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void AboutClick(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void LanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { ErrorText.Text = Localizer.Get(errorKey); if (ready) UpdateSection(); }
    private void UpdateOpacity() => OpacityValue.Text = OpacitySlider.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
}
