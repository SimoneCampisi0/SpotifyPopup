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
    private readonly DispatcherTimer volumeTimer = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private bool updatingVolume, volumePolling, volumeApplying;
    private DateTimeOffset nextVolumePoll;
    private AppSettings settings;
    private readonly AutoVisibilityPolicy visibility = new();
    private readonly StartupRegistration startup = new();
    private bool runtimeStarted, automaticallyHidden;
    private bool refreshingVisuals;
    private double? heightBeforeNotice;
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
    public bool IsTopmostPreference => settings.AlwaysOnTop;
    public bool IsNormalLayout => !settings.CompactMode && !settings.EssentialMode;
    public bool IsCompactLayout => settings.CompactMode && !settings.EssentialMode;
    public bool IsEssentialLayout => settings.EssentialMode;
    public double HeaderOpacity => !settings.EssentialMode || IsMouseOver || IsKeyboardFocusWithin ? 1 : 0;
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
        Player.PropertyChanged += PlayerChanged;
        ApplyLayout();
        ApplyPersonalization();
        volumeTimer.Tick += async (_, _) => { volumeTimer.Stop(); await ApplyVolumeAsync(); };
        timer.Tick += (_, _) =>
        {
            media.Tick();
            EvaluateVisibility();
            if (DateTimeOffset.UtcNow >= nextVolumePoll && !volumeApplying && !volumeTimer.IsEnabled)
            {
                nextVolumePoll = DateTimeOffset.UtcNow.AddSeconds(3);
                _ = PollVolumeAsync();
            }
            if (noticeUntil != default && DateTimeOffset.UtcNow >= noticeUntil) { noticeUntil = default; Player.SetNotice(""); }
        };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveSettings(); };
    }
    private void PlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerState.Notice) or nameof(PlayerState.Connected) or "") AdjustNoticeHeight();
        if (e.PropertyName is nameof(PlayerState.Album) or nameof(PlayerState.Connected) or nameof(PlayerState.Notice) or "") Changed();
    }
    private double BaseMinimumHeight => settings.EssentialMode ? 116 : settings.CompactMode ? 160 : 316;
    private void AdjustNoticeHeight()
    {
        var disconnectedCompact = settings.CompactMode && !settings.EssentialMode && !Player.Connected;
        if (!string.IsNullOrEmpty(Player.Notice) || disconnectedCompact)
        {
            heightBeforeNotice ??= Height;
            MinHeight = BaseMinimumHeight + (disconnectedCompact ? 40 : 0) + (string.IsNullOrEmpty(Player.Notice) ? 0 : 70);
            Height = Math.Max(Height, MinHeight);
        }
        else
        {
            var previous = heightBeforeNotice; heightBeforeNotice = null; MinHeight = BaseMinimumHeight;
            if (previous.HasValue) Height = Math.Max(MinHeight, previous.Value);
        }
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
            hotkeys = new HotkeyManager(source, ToggleVisibility, () => { SetLocked(false); RevealExisting(); },
                () => _ = RunCommand(MediaAction.PlayPause), () => _ = RunCommand(MediaAction.Previous), () => _ = RunCommand(MediaAction.Next),
                () => SeekRelative(-10), () => SeekRelative(10));
            if (!hotkeys.TryApply(settings, out var error)) startupWarning = error;
            SetupTray(); SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
    }
    public void Start()
    {
        if (settings.StartInTray && enableShell)
        {
            new WindowInteropHelper(this).EnsureHandle();
            if (tray != null) { visibility.Manual(false); InitializeRuntime(); return; }
        }
        Show();
    }
    internal void RevealExisting() { visibility.Manual(true); automaticallyHidden = false; ShowPopup(); }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestorePosition(); restoring = false; InitializeRuntime();
    }
    private async void InitializeRuntime()
    {
        if (runtimeStarted) return;
        runtimeStarted = true;
        if (enableShell && settings.StartWithWindows)
        {
            try { if (!startup.Matches(true)) startupWarning = TextKeys.StartupMoved; }
            catch (Exception ex) { Diagnostics.Record("startup-check", ex); startupWarning = TextKeys.StartupFailed; }
        }
        if (startupWarning != null) ShowNotice(startupWarning);
        ApplyBackground(); timer.Start();
        if (enableShell && settings.CheckUpdatesOnStartup) _ = CheckUpdatesAtStartupAsync();
        try { await media.InitializeAsync(); }
        catch (Exception ex) { Diagnostics.Record("media-load", ex); OnMediaUpdated(this, new MediaSnapshot { Status = TextKeys.MediaUnavailable }); }
    }
    private void SeekRelative(double seconds)
    {
        var snapshot = media.Snapshot;
        if (snapshot.CanSeek && snapshot.Duration > 0)
            _ = RunCommand(MediaAction.Seek, Math.Clamp((snapshot.Position + seconds) / snapshot.Duration, 0, 1), snapshot.TrackKey);
    }
    private void EvaluateVisibility()
    {
        timer.Interval = TimeSpan.FromMilliseconds(IsVisible ? 500 : 2000);
        if (settingsWindow != null) return;
        var desired = visibility.Evaluate(Player.Connected, Player.Playing, DateTimeOffset.UtcNow, settings);
        if (desired == false && IsVisible) { unlock?.Hide(); Hide(); automaticallyHidden = true; UpdateTray(); }
        else if (desired == true && automaticallyHidden) { automaticallyHidden = false; ShowPopup(false); }
    }
    private void HoverChanged(object sender, RoutedEventArgs e) => Changed();
    private void FocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e) => Changed();
    private void OnMediaUpdated(object? sender, MediaSnapshot snapshot)
    {
        if (closed) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => OnMediaUpdated(sender, snapshot))); return; }
        if (editingSlider != null && (!snapshot.CanSeek || editingTrackKey != snapshot.TrackKey)) CancelSeek();
        Player.Apply(snapshot);
        if (IsVisible && !refreshingVisuals) RefreshVisuals(snapshot);
    }
    private void RefreshVisuals(MediaSnapshot snapshot)
    {
        refreshingVisuals = true;
        try
        {
            if (settings.BackgroundMode == "artwork") _ = artworkBackground.UpdateAsync(snapshot);
            if (editingSlider == null) UpdateProgress(snapshot);
        }
        finally { refreshingVisuals = false; }
    }
    private void UpdateProgress(MediaSnapshot snapshot)
    {
        var value = snapshot.Duration > 0 ? Math.Clamp(snapshot.Position / snapshot.Duration * 100, 0, 100) : 0;
        NormalProgress.Value = CompactProgress.Value = value;
        NormalElapsed.Text = CompactElapsed.Text = PlaybackMath.Format(snapshot.Position);
    }
    private void ApplyPalette(ArtworkPalette palette)
    {
        if (closed || (palette == currentPalette && settings.AnimateBackground && !settings.ReduceMotion)) return;
        currentPalette = palette;
        void Animate(GradientStop stop, ArtworkColor color)
        {
            var from = stop.Color; // Includes the current animation value when a track interrupts it.
            var target = Color.FromArgb(250, color.R, color.G, color.B);
            stop.BeginAnimation(GradientStop.ColorProperty, null);
            // Keep the base value at the visible color until the replacement clock starts.
            stop.Color = from;
            if (!settings.AnimateBackground || settings.ReduceMotion) { stop.Color = target; return; }
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(from, target, TimeSpan.FromMilliseconds(500))
            {
                FillBehavior = FillBehavior.HoldEnd,
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
        catch (Exception ex) { Diagnostics.Record("player-command", ex); ShowNotice(TextKeys.CommandFailed); }
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
            var button = new Button { Content = "\uE785", ToolTip = Localizer.Get(TextKeys.UnlockPopup), Style = (Style)FindResource("GlassButton"), Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(35, 43, 48)) };
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
    private WindowPlacement Placement => settings.EssentialMode ? settings.Essential : settings.CompactMode ? settings.Compact : settings.Normal;
    private void CapturePlacement()
    {
        if (restoring || !IsLoaded || WindowState != WindowState.Normal) return;
        var bounds = NativeMethods.Bounds(this);
        Placement.X = bounds.Left; Placement.Y = bounds.Top; Placement.Width = ActualWidth; Placement.Height = heightBeforeNotice ?? ActualHeight;
    }
    private void ApplyLayout()
    {
        var compact = settings.CompactMode; var essential = settings.EssentialMode;
        MinWidth = essential ? 280 : 360; MinHeight = BaseMinimumHeight;
        NormalLayout.Visibility = !compact && !essential ? Visibility.Visible : Visibility.Collapsed;
        CompactLayout.Visibility = compact && !essential ? Visibility.Visible : Visibility.Collapsed;
        EssentialLayout.Visibility = essential ? Visibility.Visible : Visibility.Collapsed;
        heightBeforeNotice = null;
        Width = Math.Max(MinWidth, Placement.Width); Height = Math.Max(MinHeight, Placement.Height);
        AdjustNoticeHeight(); Changed();
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
        settings.EssentialMode = false; settings.CompactMode = !settings.CompactMode; ApplyLayout(); UpdateLayout(); RestorePosition();
        restoring = false; QueueSave();
    }
    private void NormalLayoutClick(object sender, RoutedEventArgs e) => SelectLayout(false, false);
    private void CompactLayoutClick(object sender, RoutedEventArgs e) => SelectLayout(true, false);
    private void EssentialLayoutClick(object sender, RoutedEventArgs e) => SelectLayout(false, true);
    private void SelectLayout(bool compact, bool essential)
    {
        if (settings.CompactMode == compact && settings.EssentialMode == essential) { Changed(); return; }
        CapturePlacement(); restoring = true; settings.CompactMode = compact; settings.EssentialMode = essential;
        ApplyLayout(); UpdateLayout(); RestorePosition(); restoring = false; QueueSave();
    }
    private void EssentialClick(object sender, RoutedEventArgs e)
    {
        CapturePlacement(); restoring = true;
        settings.EssentialMode = !settings.EssentialMode; settings.CompactMode = false;
        ApplyLayout(); UpdateLayout(); RestorePosition(); restoring = false; QueueSave();
    }
    private void ResetPlacement()
    {
        settings.Normal = new(); settings.Compact = new() { Width = 460, Height = 166 };
        settings.Essential = new() { Width = 330, Height = 116 };
        restoring = true; ApplyLayout(); WindowStartupLocation = WindowStartupLocation.Manual;
        var area = Forms.Screen.PrimaryScreen!.WorkingArea;
        NativeMethods.Move(this, area.Left + 30, area.Top + 30); NativeMethods.EnsureVisible(this);
        restoring = false; PositionUnlock(); SaveSettings();
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
            if (!Forms.SystemInformation.HighContrast)
            {
                menu.Renderer = new Forms.ToolStripProfessionalRenderer(new TrayColors()) { RoundedEdges = true };
                menu.ForeColor = System.Drawing.Color.FromArgb(243, 245, 247);
                menu.ShowImageMargin = false; menu.Padding = new Forms.Padding(6);
            }
            trayVisibility = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Hide), null, (_, _) => Dispatch(ToggleVisibility));
            trayLock = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Lock), null, (_, _) => Dispatch(() => { visibility.Manual(true); SetLocked(!locked); ShowPopup(false); }));
            menu.Items.Add(trayVisibility); menu.Items.Add(trayLock);
            traySettings = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Settings), null, (_, _) => Dispatch(OpenSettings));
            trayExit = new Forms.ToolStripMenuItem(Localizer.Get(TextKeys.Exit), null, (_, _) => Dispatch(Close));
            menu.Items.Add(traySettings);
            menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(trayExit);
            foreach (Forms.ToolStripItem item in menu.Items) item.Padding = new Forms.Padding(10, 6, 10, 6);
            tray = new Forms.NotifyIcon { Text = "Spotify Popup", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += (_, _) => Dispatch(RevealExisting);
            UpdateTray();
        }
        catch (Exception ex) { Diagnostics.Record("tray", ex); ShowNotice(TextKeys.TrayFailed); }
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
        var show = !IsVisible || WindowState == WindowState.Minimized;
        visibility.Manual(show); automaticallyHidden = false;
        if (!show) { unlock?.Hide(); Hide(); } else ShowPopup();
        UpdateTray();
    }
    private void ShowPopup(bool activate = true)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show(); NativeMethods.EnsureVisible(this); RefreshVisuals(media.Snapshot);
        if (locked && unlock != null) { unlock.Show(); PositionUnlock(); }
        if (activate && !locked) Activate();
        UpdateTray();
    }
    private void HideClick(object sender, RoutedEventArgs e) => ToggleVisibility();
    private void OpenMenuClick(object sender, RoutedEventArgs e)
    {
        var menu = RootSurface.ContextMenu;
        menu.PlacementTarget = (Button)sender; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenSettings()
    {
        visibility.Manual(true); automaticallyHidden = false; SetLocked(false); ShowPopup();
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        CapturePlacement();
        var originalOpacity = settings.LockedOpacity;
        settingsWindow = new SettingsWindow(settings, value => { Opacity = value; },
            (_, _) => ValidateSettings(settingsWindow!.Draft), ResetPlacement);
        settingsWindow.Owner = this;
        var dialog = settingsWindow;
        hotkeys?.Suspend();
        try
        {
            if (dialog.ShowDialog() == true)
            {
                settings = dialog.Draft; settings.Normalize(); Localizer.Current.Apply(settings.Language);
                if (dialog.ResetRequested) { ApplyLayout(); ResetPlacement(); }
                else if (dialog.ResetPlacementRequested) ResetPlacement();
                visibility.Reset(); ApplyPersonalization(); ApplyBackground(); Changed(); SaveSettings();
            }
            else settings.LockedOpacity = originalOpacity;
        }
        finally
        {
            settingsWindow = null; Opacity = 1;
            if (!closed && hotkeys != null && !hotkeys.TryApply(settings, out var restoreError)) ShowNotice(restoreError);
        }
    }
    private string? ValidateSettings(AppSettings candidate)
    {
        if (hotkeys != null && !hotkeys.TryApply(candidate, out var error)) return error;
        if (enableShell && !startup.TryApply(candidate.StartWithWindows))
        {
            hotkeys?.Suspend(); return TextKeys.StartupFailed;
        }
        return null;
    }
    private void ApplyBackground()
    {
        if (settings.BackgroundMode == "artwork") { artworkBackground.Invalidate(); RefreshVisuals(media.Snapshot); return; }
        artworkBackground.Invalidate();
        var palette = ArtworkPalette.Default;
        if (settings.BackgroundMode == "custom")
        {
            var color = (Color)ColorConverter.ConvertFromString(settings.BackgroundColor);
            palette = ArtworkPaletteExtractor.Extract(new byte[] { color.B, color.G, color.R, 255 });
        }
        // Force applying a palette when transitions have just been disabled.
        currentPalette = new(new(255, 255, 255), new(255, 255, 255)); ApplyPalette(palette);
    }

    private void ApplyPersonalization()
    {
        Topmost = settings.AlwaysOnTop;
        var size = 96 * settings.CoverSizePercent / 100d;
        NormalArtwork.Width = NormalArtwork.Height = size;
        // A merged theme brush can be frozen by WPF. Override the resource instead of mutating it.
        Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.AccentColor));
        Changed();
    }
    private void ToggleTopmostClick(object sender, RoutedEventArgs e)
    {
        settings.AlwaysOnTop = !settings.AlwaysOnTop;
        ApplyPersonalization();
        SaveSettings();
    }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!runtimeStarted || updatingVolume || !Player.Connected) return;
        volumeTimer.Stop();
        volumeTimer.Start();
    }
    private async Task PollVolumeAsync()
    {
        if (closed || volumePolling) return;
        volumePolling = true;
        try
        {
            var volume = Player.Connected ? await Task.Run(SpotifyVolumeService.Read) : null;
            if (closed || volumeApplying || volumeTimer.IsEnabled) return;
            updatingVolume = true;
            VolumeSlider.IsEnabled = volume.HasValue;
            if (volume.HasValue) VolumeSlider.Value = volume.Value * 100;
        }
        catch (Exception ex) { Diagnostics.Record("volume-poll", ex); if (!closed) VolumeSlider.IsEnabled = false; }
        finally { updatingVolume = false; volumePolling = false; }
    }
    private async Task ApplyVolumeAsync()
    {
        if (closed || volumeApplying || !Player.Connected) return;
        volumeApplying = true;
        try
        {
            var value = (float)(VolumeSlider.Value / 100);
            if (!await Task.Run(() => SpotifyVolumeService.Write(value)) && !closed)
                ShowNotice(TextKeys.VolumeUnavailable);
        }
        catch (Exception ex) { Diagnostics.Record("volume-set", ex); if (!closed) ShowNotice(TextKeys.VolumeUnavailable); }
        finally { volumeApplying = false; nextVolumePoll = DateTimeOffset.UtcNow.AddSeconds(1); }
    }
    private async Task CheckUpdatesAtStartupAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var latest = await new UpdateService(client).LatestAsync(timeout.Token);
            if (closed || !settings.CheckUpdatesOnStartup) return;
            var installed = typeof(MainWindow).Assembly.GetName().Version!;
            if (latest > new Version(installed.Major, installed.Minor, installed.Build))
                ShowNotice(TextKeys.NewUpdateNotice);
        }
        catch (Exception ex) { Diagnostics.Record("update-startup", ex); }
    }
    private void OpenSpotifyClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true }); }
        catch (Exception ex) { Diagnostics.Record("open-spotify", ex); ShowNotice(TextKeys.SpotifyOpenFailed); }
    }
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosed(object? sender, EventArgs e)
    {
        if (closed) return;
        CapturePlacement(); store.Save(settings); closed = true;
        artworkBackground.Dispose();
        BackgroundStart.BeginAnimation(GradientStop.ColorProperty, null); BackgroundEnd.BeginAnimation(GradientStop.ColorProperty, null);
        timer.Stop(); saveTimer.Stop(); volumeTimer.Stop(); media.Updated -= OnMediaUpdated; media.Notice -= OnNotice; media.Dispose();
        Localizer.Current.PropertyChanged -= LanguageChanged; Player.PropertyChanged -= PlayerChanged; Player.Dispose();
        if (enableShell) SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        hotkeys?.Dispose(); source?.RemoveHook(WindowHook); unlock?.Close();
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayIcon?.Dispose();
    }
}
