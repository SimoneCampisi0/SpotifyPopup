using SpotifyPopup;

var count = 0;
void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{name}: expected {expected}, got {actual}");
    Console.WriteLine($"PASS {name}"); count++;
}
Equal(15d, PlaybackMath.Position(110, 100, 60, 5, 1, true), "Nonzero timeline origin is subtracted");
Equal(10d, PlaybackMath.Position(110, 100, 60, 5, 1, false), "Paused timeline does not advance");
Equal(20d, PlaybackMath.Position(110, 100, 60, 5, 2, true), "Playback rate is respected");
Equal(60d, PlaybackMath.Position(159, 100, 60, 5, 1, true), "Position cannot exceed duration");
Equal(0d, PlaybackMath.Position(90, 100, 60, 0, 1, true), "Position cannot be negative");
Equal(10d, PlaybackMath.Position(110, 100, 60, -100, 1, true), "Future update timestamps do not rewind playback");
Equal(0d, PlaybackMath.Position(double.NaN, 0, 60, 1, 1, true), "Invalid position is handled");
Equal(130L, PlaybackMath.SeekTicks(100, 60, .5), "Seek uses absolute origin");
Equal(160L, PlaybackMath.SeekTicks(100, 60, 2), "Seek is clamped to track end");
Equal(100L, PlaybackMath.SeekTicks(100, 60, double.NaN), "Invalid seek fraction is handled");
Equal("1:02:03", PlaybackMath.Format(3723), "Long tracks display hours");
Equal("0:00", PlaybackMath.Format(-1), "Invalid duration has a safe display");
var directory = Path.GetFullPath(".temp/logic-checks"); Directory.CreateDirectory(directory);
var path = Path.Combine(directory, Guid.NewGuid() + ".json");
var store = new SettingsStore(path);
var settings = store.Load(out var warning);
Equal<string?>(null, warning, "First run needs no existing settings");
settings.Normal.X = -1500; settings.Normal.Y = 100; settings.CompactMode = true;
settings.LockedOpacity = .72; settings.ShowAlbum = false; settings.ToggleShortcut = "Ctrl+Alt+F8";
Equal(true, store.Save(settings), "Settings can be saved atomically");
var loaded = store.Load(out warning);
Equal(-1500, loaded.Normal.X, "Negative coordinates on a secondary monitor survive round trip");
Equal(.72, loaded.LockedOpacity, "Opacity survives restart");
Equal(false, loaded.ShowAlbum, "Album preference survives restart");
Equal(true, loaded.CompactMode, "Layout preference survives restart");
File.WriteAllText(path, "{broken");
loaded = store.Load(out warning);
Equal(true, warning != null, "Corrupt settings report a recoverable warning");
Equal(.58, loaded.LockedOpacity, "Corrupt settings fall back to defaults");
loaded.Normal.Width = double.NaN; loaded.Compact.Height = -100; loaded.LockedOpacity = 99;
loaded.Normalize();
Equal(390d, loaded.Normal.Width, "Nonfinite dimensions are repaired");
Equal(138d, loaded.Compact.Height, "Dimensions respect compact minimum");
Equal(1d, loaded.LockedOpacity, "Opacity cannot exceed one");
Console.WriteLine($"{count} logic checks passed.");
