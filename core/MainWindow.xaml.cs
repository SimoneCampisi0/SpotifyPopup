using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SpotifyPopup;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IMediaController media;
    private readonly ArtworkBackgroundService artworkBackground;
    private ArtworkPalette currentPalette = ArtworkPalette.Default;
    private readonly SettingsStore store;
    private readonly bool enableShell;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly AppSettings settings;
    private HwndSource? source;
    private HotkeyManager? hotkeys;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? trayVisibility, trayLock, traySettings, trayExit;
    private System.Drawing.Icon? trayIcon;
    private Window? unlock;
    private SettingsWindow? settingsWindow;
    private bool locked, closed, restoring = true, runningCommand;
    private Slider? editingSlider;
    private string editingTrackKey = "";
    private DateTimeOffset noticeUntil;
    private string? startupWarning;
    public PlayerState Player { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public Visibility AlbumVisibility => settings.ShowAlbum && !string.IsNullOrEmpty(Player.Album) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OpenSpotifyVisibility => Player.Connected ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoticeVisibility => string.IsNullOrEmpty(Player.Notice) ? Visibility.Collapsed : Visibility.Visible;

    public MainWindow() : this(null, null, true) { }
    public MainWindow(IMediaController? controller, SettingsStore? settingsStore, bool enableShell)
    {
        this.enableShell = enableShell; store = settingsStore ?? new SettingsStore(); settings = store.Load(out startupWarning);
        Localizer.Current.Apply(settings.Language);
        InitializeComponent(); DataContext = this;
        artworkBackground = new ArtworkBackgroundService(Dispatcher, ApplyPalette);
        Localizer.Current.PropertyChanged += LanguageChanged;
        media = controller ?? new MediaController(Dispatcher);
        media.Updated += OnMediaUpdated; media.Notice += OnNotice;
        Player.PropertyChanged += (_, _) => { MinHeight = settings.CompactMode ? (string.IsNullOrEmpty(Player.Notice) ? 138 : 190) : 255; Changed(); };
        ApplyLayout();
        timer.Tick += (_, _) =>
        {
            media.Tick();
            if (noticeUntil != default && DateTimeOffset.UtcNow >= noticeUntil) { noticeUntil = default; Player.SetNotice(""); }
        };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveSettings(); };
    }
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTray();
        if (unlock?.Content is Button button)
        {
            button.ToolTip = Localizer.Get(TextKeys.UnlockPopup);
            System.Windows.Automation.AutomationProperties.SetName(button, Localizer.Get(TextKeys.UnlockPopup));
        }
        PositionUnlock();
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WindowHook);
        if (enableShell && source != null)
        {
            hotkeys = new HotkeyManager(source, ToggleVisibility, () => { SetLocked(false); ShowPopup(); });
            if (!hotkeys.TryApply(settings.ToggleShortcut, settings.UnlockShortcut, out var error)) startupWarning = error;
        }
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestorePosition(); restoring = false;
        if (enableShell) { SetupTray(); SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged; }
        if (startupWarning != null) ShowNotice(startupWarning);
        timer.Start();
        try { await media.InitializeAsync(); }
        catch (Exception) { OnMediaUpdated(this, new MediaSnapshot { Status = TextKeys.MediaUnavailable }); }
    }
    private void OnMediaUpdated(object? sender, MediaSnapshot snapshot)
    {
        if (closed) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => OnMediaUpdated(sender, snapshot))); return; }
        if (editingSlider != null && (!snapshot.CanSeek || editingTrackKey != snapshot.TrackKey)) CancelSeek();
        Player.Apply(snapshot);
        _ = artworkBackground.UpdateAsync(snapshot);
        if (editingSlider == null) UpdateProgress(snapshot);
    }
    private void UpdateProgress(MediaSnapshot snapshot)
    {
        var value = snapshot.Duration > 0 ? Math.Clamp(snapshot.Position / snapshot.Duration * 100, 0, 100) : 0;
        NormalProgress.Value = CompactProgress.Value = value;
        NormalElapsed.Text = CompactElapsed.Text = PlaybackMath.Format(snapshot.Position);
    }
    private void ApplyPalette(ArtworkPalette palette)
    {
        if (closed || palette == currentPalette) return;
        currentPalette = palette;
        static void Animate(GradientStop stop, ArtworkColor color)
        {
            var from = stop.Color; // Includes the current animation value when a track interrupts it.
            var target = Color.FromArgb(243, color.R, color.G, color.B);
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            stop.Color = target;
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(from, target, TimeSpan.FromMilliseconds(500))
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            }, HandoffBehavior.SnapshotAndReplace);
        }
        Animate(BackgroundStart, palette.Start); Animate(BackgroundEnd, palette.End);
    }
    private void OnNotice(object? sender, string message)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => ShowNotice(message))); return; }
        ShowNotice(message);
    }
    private void ShowNotice(string message)
    {
        if (closed) return;
        Player.SetNotice(message); noticeUntil = DateTimeOffset.UtcNow.AddSeconds(8);
    }
    private async Task RunCommand(MediaAction action, double fraction = 0, string? key = null)
    {
        if (closed || runningCommand) return;
        runningCommand = true; Player.SetWorking(true);
        try { await media.ExecuteAsync(action, fraction, key); }
        catch (Exception) { ShowNotice(TextKeys.CommandFailed); }
        finally { runningCommand = false; if (!closed) { Player.SetWorking(false); UpdateProgress(media.Snapshot); } }
    }
    private async void PlayClick(object sender, RoutedEventArgs e) => await RunCommand(MediaAction.PlayPause);
    private async void PreviousClick(object sender, RoutedEventArgs e) => await RunCommand(MediaAction.Previous);
    private async void NextClick(object sender, RoutedEventArgs e) => await RunCommand(MediaAction.Next);
    private async void RestartClick(object sender, RoutedEventArgs e) => await RunCommand(MediaAction.Seek, 0, media.Snapshot.TrackKey);
    private void BeginSeek(Slider slider)
    {
        if (!Player.CanSeek) return;
        editingSlider = slider; editingTrackKey = media.Snapshot.TrackKey;
    }
    private void SeekMouseDown(object sender, MouseButtonEventArgs e) => BeginSeek((Slider)sender);
    private void SeekMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Commit after the Slider/Thumb has processed the final mouse position.
        Dispatcher.BeginInvoke(new Action(async () => await CommitSeekAsync((Slider)sender)), DispatcherPriority.Input);
    }
    private void SeekCaptureLost(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Released) { CancelSeek(); return; }
        Dispatcher.BeginInvoke(new Action(async () => await CommitSeekAsync((Slider)sender)), DispatcherPriority.Input);
    }
    private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;
    private void SeekKeyDown(object sender, KeyEventArgs e) { if (IsSeekKey(e.Key)) BeginSeek((Slider)sender); }
    private async void SeekKeyUp(object sender, KeyEventArgs e) { if (IsSeekKey(e.Key)) await CommitSeekAsync((Slider)sender); }
    private async void SeekKeyboardFocusLost(object sender, KeyboardFocusChangedEventArgs e) => await CommitSeekAsync((Slider)sender);
    private void SeekValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (editingSlider != sender) return;
        var text = PlaybackMath.Format(media.Snapshot.Duration * e.NewValue / 100);
        NormalElapsed.Text = CompactElapsed.Text = text;
    }
    private async Task CommitSeekAsync(Slider slider)
    {
        if (editingSlider != slider || closed) return;
        var fraction = slider.Value / 100; var key = editingTrackKey;
        editingSlider = null;
        await RunCommand(MediaAction.Seek, fraction, key);
    }
    private void CancelSeek() { editingSlider = null; UpdateProgress(media.Snapshot); }
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (locked || e.ButtonState != MouseButtonState.Pressed) return;
        for (DependencyObject? item = e.OriginalSource as DependencyObject; item != null && item != RootSurface; item = GetVisualParent(item))
            if (item is ButtonBase or Slider or Thumb or ResizeGrip) return;
        try { DragMove(); NativeMethods.EnsureVisible(this, settings.SnapToEdges); QueueSave(); }
        catch (InvalidOperationException) { /* The button can be released before DragMove starts. */ }
    }
    private static DependencyObject? GetVisualParent(DependencyObject item) => item is Visual ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item);
    private void LockClick(object sender, RoutedEventArgs e) => SetLocked(true);
    internal void SetLocked(bool value)
    {
        if (closed || locked == value) return;
        locked = value; Opacity = value ? settings.LockedOpacity : 1;
        foreach (var tooltip in Descendants<ToolTip>(RootSurface)) tooltip.IsOpen = false;
        foreach (var text in Descendants<TextBlock>(RootSurface))
        {
            ToolTipService.SetIsEnabled(text, !value);
            if (text.ToolTip is ToolTip tooltip) tooltip.IsOpen = false;
        }
        if (value) { CancelSeek(); Keyboard.ClearFocus(); }
        RootSurface.IsEnabled = !value;
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        var mask = NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle, value ? style | mask : style & ~mask);
        if (value)
        {
            var button = new Button { Content = "\uE785", ToolTip = Localizer.Get(TextKeys.UnlockPopup), Style = (Style)FindResource("GlassButton"), Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(39, 57, 52)) };
            System.Windows.Automation.AutomationProperties.SetName(button, Localizer.Get(TextKeys.UnlockPopup));
            unlock = new Window { Width = 32, Height = 32, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = true, Content = button, Owner = this };
            button.Click += (_, _) => SetLocked(false);
            if (IsVisible) { unlock.Show(); PositionUnlock(); }
        }
        else { unlock?.Close(); unlock = null; }
        UpdateTray();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private void PositionUnlock()
    {
        if (unlock == null || !unlock.IsVisible || !IsVisible) return;
        var point = LockButton.PointToScreen(new Point(0, 0));
        NativeMethods.Move(unlock, (int)Math.Round(point.X), (int)Math.Round(point.Y));
    }
    private WindowPlacement Placement => settings.CompactMode ? settings.Compact : settings.Normal;
    private void CapturePlacement()
    {
        if (restoring || !IsLoaded || WindowState != WindowState.Normal) return;
        var bounds = NativeMethods.Bounds(this);
        Placement.X = bounds.Left; Placement.Y = bounds.Top; Placement.Width = ActualWidth; Placement.Height = ActualHeight;
    }
    private void ApplyLayout()
    {
        var compact = settings.CompactMode;
        MinWidth = compact ? 360 : 330; MinHeight = compact ? (string.IsNullOrEmpty(Player.Notice) ? 138 : 190) : 255;
        NormalLayout.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactLayout.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        Width = Math.Max(MinWidth, Placement.Width); Height = Math.Max(MinHeight, Placement.Height);
        Changed();
    }
    private void RestorePosition()
    {
        if (Placement.X is int x && Placement.Y is int y) NativeMethods.Move(this, x, y);
        NativeMethods.EnsureVisible(this); PositionUnlock();
    }
    private void CompactClick(object sender, RoutedEventArgs e) => ToggleCompact();
    internal void ToggleCompact()
    {
        CapturePlacement(); restoring = true;
        settings.CompactMode = !settings.CompactMode; ApplyLayout(); UpdateLayout(); RestorePosition();
        restoring = false; QueueSave();
    }
    private void OnLocationChanged(object? sender, EventArgs e) { PositionUnlock(); QueueSave(); }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) { PositionUnlock(); QueueSave(); }
    private void QueueSave() { if (restoring || closed) return; saveTimer.Stop(); saveTimer.Start(); }
    private void SaveSettings()
    {
        CapturePlacement();
        if (!store.Save(settings)) ShowNotice(TextKeys.SettingsSaveFailed);
    }
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(new Action(() => { if (!closed) { NativeMethods.EnsureVisible(this); PositionUnlock(); QueueSave(); } }));
    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is 0x02E0 or 0x007E) Dispatcher.BeginInvoke(new Action(() => { if (!closed && IsLoaded) { NativeMethods.EnsureVisible(this); PositionUnlock(); } }), DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }
    private void SetupTray()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/SpotifyPopup;component/Assets/logo.ico"));
            using var stream = resource!.Stream;
            using var embeddedIcon = new System.Drawing.Icon(stream);
            trayIcon = (System.Drawing.Icon)embeddedIcon.Clone();
            var menu = new Forms.ContextMenuStrip();
            trayVisibility = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Hide), null, (_, _) => Dispatch(ToggleVisibility));
            trayLock = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Lock), null, (_, _) => Dispatch(() => { SetLocked(!locked); ShowPopup(false); }));
            menu.Items.Add(trayVisibility); menu.Items.Add(trayLock);
            traySettings = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Settings), null, (_, _) => Dispatch(OpenSettings));
            trayExit = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Exit), null, (_, _) => Dispatch(Close));
            menu.Items.Add(traySettings);
            menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(trayExit);
            tray = new Forms.NotifyIcon { Text = "Spotify Popup", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => Dispatch(() => ShowPopup());
            UpdateTray();
        }
        catch (Exception) { ShowNotice(TextKeys.TrayFailed); }
    }
    private void Dispatch(Action action) { if (!closed) Dispatcher.BeginInvoke(action); }
    private void UpdateTray()
    {
        if (trayVisibility != null) trayVisibility.Text = IsVisible ? Localizer.Get(TextKeys.Hide) : Localizer.Get(TextKeys.Show);
        if (trayLock != null) trayLock.Text = locked ? Localizer.Get(TextKeys.Unlock) : Localizer.Get(TextKeys.Lock);
        if (traySettings != null) traySettings.Text = Localizer.Get(TextKeys.Settings);
        if (trayExit != null) trayExit.Text = Localizer.Get(TextKeys.Exit);
    }
    private void ToggleVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized) { unlock?.Hide(); Hide(); }
        else ShowPopup();
        UpdateTray();
    }
    private void ShowPopup(bool activate = true)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show(); NativeMethods.EnsureVisible(this);
        if (locked && unlock != null) { unlock.Show(); PositionUnlock(); }
        if (activate && !locked) Activate();
        UpdateTray();
    }
    private void HideClick(object sender, RoutedEventArgs e) => ToggleVisibility();
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenSettings()
    {
        SetLocked(false); ShowPopup();
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        var originalOpacity = settings.LockedOpacity;
        settingsWindow = new SettingsWindow(settings, value => { Opacity = value; },
            (toggle, unlockValue) => hotkeys == null ? null : hotkeys.TryApply(toggle, unlockValue, out var error) ? null : error);
        settingsWindow.Owner = this;
        var dialog = settingsWindow;
        try
        {
            if (dialog.ShowDialog() == true)
            {
                settings.ShowAlbum = dialog.ShowAlbum; settings.SnapToEdges = dialog.SnapToEdges;
                settings.LockedOpacity = dialog.LockedOpacity; settings.ToggleShortcut = dialog.ToggleShortcut; settings.UnlockShortcut = dialog.UnlockShortcut;
                settings.Language = dialog.SelectedLanguage; Localizer.Current.Apply(settings.Language);
                Changed(); SaveSettings();
            }
            else settings.LockedOpacity = originalOpacity;
        }
        finally { settingsWindow = null; Opacity = 1; }
    }
    private void OpenSpotifyClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true }); }
        catch (Exception) { ShowNotice(TextKeys.SpotifyOpenFailed); }
    }
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosed(object? sender, EventArgs e)
    {
        if (closed) return;
        CapturePlacement(); store.Save(settings); closed = true;
        artworkBackground.Dispose();
        BackgroundStart.BeginAnimation(GradientStop.ColorProperty, null); BackgroundEnd.BeginAnimation(GradientStop.ColorProperty, null);
        timer.Stop(); saveTimer.Stop(); media.Updated -= OnMediaUpdated; media.Notice -= OnNotice; media.Dispose();
        Localizer.Current.PropertyChanged -= LanguageChanged; Player.Dispose();
        if (enableShell) SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        hotkeys?.Dispose(); source?.RemoveHook(WindowHook); unlock?.Close();
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayIcon?.Dispose();
    }
}
