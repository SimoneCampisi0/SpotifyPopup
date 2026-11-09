using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SpotifyPopup;

internal static class DesignChecks
{
    internal static async Task<int> Run(string output)
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        async Task Pump() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
        var store = new SettingsStore(System.IO.Path.Combine(output, "design-player.json"));
        store.Save(new AppSettings { Language = "en", AnimateBackground = false });
        var controller = new Fake(); var player = new MainWindow(controller, store, false); player.Show();
        controller.Publish(new MediaSnapshot { Title = "Slow Motion", Artist = "Nights & Days", Album = "After Hours · 2026", Artwork = Cover(),
            Connected = true, Playing = true, Status = TextKeys.Playing, CanPlayPause = true, CanPrevious = true, CanNext = true, CanSeek = true, Duration = 242, Position = 87, TrackKey = "design" });
        await Pump(); await Task.Delay(100);
        Save(player, System.IO.Path.Combine(output, "design-player.png"));
        var title = (TextBlock)player.FindName("NormalTitle");
        var originalFont = title.FontSize;
        player.Width += 200; player.Height += 80; await Pump();
        Check(title.FontSize == originalFont && originalFont == 22, "Resizing preserves text size instead of scaling the complete player");
        player.Width = player.MinWidth; player.Height = player.MinHeight; await Pump();
        var metadata = (Grid)player.FindName("NormalMetadata");
        Check(metadata.ActualHeight >= 96, "Minimum player height preserves full cover artwork");
        Save(player, System.IO.Path.Combine(output, "design-player-minimum.png"));
        Save(player, System.IO.Path.Combine(output, "design-player-2x.png"), 2);
        var progress = (Slider)player.FindName("NormalProgress");
        Check(progress.Template.FindName("PART_Track", progress) is Track, "Custom seek slider retains its native track and keyboard behavior");
        var play = (Button)player.FindName("PlayButton");
        Check(play.Template.FindName("Surface", play) is Border, "Player uses the shared rounded button template");
        ((Button)player.FindName("MenuButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
        var menu = ((Border)player.FindName("RootSurface")).ContextMenu;
        Check(menu.IsOpen && menu.Items.OfType<MenuItem>().Count(i => i.IsCheckable && i.IsChecked) == 1, "Player menu opens with exactly one active layout");
        Save(menu, System.IO.Path.Combine(output, "design-player-menu.png")); menu.IsOpen = false;
        player.ToggleCompact(); player.Width = 420; player.Height = player.MinHeight; await Pump();
        Save(player, System.IO.Path.Combine(output, "design-compact.png"));
        controller.Publish(controller.Snapshot with { Connected = false, Title = "", Artist = "", Album = "", Artwork = null, CanPlayPause = false, CanSeek = false }); await Pump();
        Check(player.Height >= 200, "Compact disconnect creates enough room for the Open Spotify action");
        controller.Publish(controller.Snapshot with { Connected = true, Title = "Slow Motion", Artist = "Nights & Days", Artwork = Cover() }); await Pump();
        Check(player.Height == 160, "Compact reconnect restores the user height after a temporary disconnect");
        player.Close();
        foreach (var language in new[] { "en", "it" })
        {
            Localizer.Current.Apply(language);
            var settings = new SettingsWindow(new AppSettings { Language = language }, _ => { }, (_, _) => null);
            settings.Show(); settings.Width = settings.MinWidth; settings.Height = settings.MinHeight;
            for (var page = 0; page < 5; page++)
            {
                settings.SelectSection(page); await Pump();
                var scroll = (ScrollViewer)settings.FindName("SettingsScroll");
                Check(scroll.ExtentWidth <= scroll.ViewportWidth + 1, $"Settings section {page} has no horizontal clipping: {language}");
                Save(settings, System.IO.Path.Combine(output, $"design-settings-{language}-{page}.png"));
            }
            settings.SelectSection(1);
            var background = (ComboBox)settings.FindName("BackgroundBox"); background.SelectedValue = "custom"; await Pump();
            Check(((StackPanel)settings.FindName("CustomColorPanel")).IsVisible, "Custom color input is revealed only when relevant");
            ((TextBox)settings.FindName("ColorBox")).Text = "#EC885C"; await Pump();
            Check(((SolidColorBrush)((Border)settings.FindName("ColorSwatch")).Background).Color.R == 236, "Color swatch follows valid hex input without applying player settings");
            Save(settings, System.IO.Path.Combine(output, $"design-custom-color-{language}.png"));
            background.SelectedValue = "fixed"; await Pump();
            Check(!((StackPanel)settings.FindName("CustomColorPanel")).IsVisible && !((CheckBox)settings.FindName("AnimationBox")).IsEnabled, "Fixed theme hides irrelevant color and animation controls");
            settings.SelectSection(2); await Pump();
            Check(!((TextBox)settings.FindName("DelayBox")).IsEnabled, "Pause delay is disabled until automatic pause hiding is enabled");
            ((CheckBox)settings.FindName("PausedBox")).IsChecked = true; await Pump();
            Check(((TextBox)settings.FindName("DelayBox")).IsEnabled, "Pause hiding enables its delay field");
            Check(((TextBox)settings.FindName("DelayBox")).ActualHeight <= 44, "Input padding is applied once and preserves a compact control height");
            settings.SelectSection(4); ((Button)settings.FindName("ResetAllButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
            Check(((Border)settings.FindName("ResetConfirmation")).IsVisible && !settings.ResetRequested, "Reset asks for an inline confirmation before changing any preferences");
            Check(((Button)settings.FindName("ConfirmResetButton")).IsDefault && !((Button)settings.FindName("SaveButton")).IsDefault, "Enter targets confirmation rather than silently saving the dialog");
            settings.RaiseEvent(new KeyEventArgs(InputManager.Current.PrimaryKeyboardDevice, PresentationSource.FromVisual(settings), Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(!((Border)settings.FindName("ResetConfirmation")).IsVisible && settings.IsVisible, "Escape dismisses reset confirmation while keeping Settings open");
            ((Button)settings.FindName("ResetAllButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
            Save(settings, System.IO.Path.Combine(output, $"design-reset-confirm-{language}.png"));
            ((Button)settings.FindName("ConfirmResetButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(settings.ResetRequested && !settings.Draft.StartWithWindows, "Confirmed reset fills defaults and still requires Save");
            settings.Close();
            var about = new AboutWindow(); about.Show(); await Pump();
            Check(((TextBox)about.FindName("LicenseText")).Visibility == Visibility.Collapsed, "About keeps the full offline license behind an explicit disclosure");
            Save(about, System.IO.Path.Combine(output, $"design-about-{language}.png"));
            ((Button)about.FindName("LicenseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Pump();
            Check(((TextBox)about.FindName("LicenseText")).IsVisible, "License disclosure remains accessible"); about.Close();
        }
        return count;
    }
    private static BitmapSource Cover()
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(211, 116, 78)), null, new Rect(0, 0, 288, 288));
            drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(244, 206, 154)), null, new Point(195, 98), 94, 94);
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(33, 55, 71)), null, new Rect(0, 175, 288, 113));
            drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(33, 55, 71)), null, new Point(42, 192), 100, 85);
            drawing.DrawText(new FormattedText("SLOW\nMOTION", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 28, Brushes.White, 1), new Point(20, 203));
        }
        var bitmap = new RenderTargetBitmap(288, 288, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static void Save(FrameworkElement visual, string path, double scale = 1)
    {
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(visual.ActualWidth * scale)), Math.Max(1, (int)Math.Ceiling(visual.ActualHeight * scale)), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path); encoder.Save(stream);
    }
    private sealed class Fake : IMediaController
    {
        public MediaSnapshot Snapshot { get; private set; } = new();
        public event EventHandler<MediaSnapshot>? Updated;
        public event EventHandler<string>? Notice { add { } remove { } }
        public Task InitializeAsync() => Task.CompletedTask;
        public void Tick() { }
        public void Publish(MediaSnapshot value) { Snapshot = value; Updated?.Invoke(this, value); }
        public Task ExecuteAsync(MediaAction action, double fraction = 0, string? key = null) => Task.CompletedTask;
        public void Dispose() { }
    }
}
