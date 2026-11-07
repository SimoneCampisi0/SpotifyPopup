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
// Language migration must not discard existing preferences.
File.WriteAllText(path, "{\"ShowAlbum\":false,\"LockedOpacity\":0.72,\"ToggleShortcut\":\"Ctrl+Alt+F8\"}");
loaded = store.Load(out warning);
Equal("auto", loaded.Language, "Legacy JSON defaults to system language");
Equal(false, loaded.ShowAlbum, "Legacy album preference is preserved");
Equal("Ctrl+Alt+F8", loaded.ToggleShortcut, "Legacy shortcut is preserved");
foreach (var invalid in new string?[] { null, "", "fr", "IT", "unknown" })
{
    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { Language = invalid, LockedOpacity = .72 }));
    loaded = store.Load(out warning);
    Equal("auto", loaded.Language, "Invalid language normalizes: " + invalid);
    Equal(.72, loaded.LockedOpacity, "Invalid language preserves other preferences");
}
foreach (var language in new[] { "auto", "en", "it" })
{
    loaded.Language = language;
    Equal(true, store.Save(loaded), "Save language " + language);
    Equal(language, store.Load(out _).Language, "Language round trip " + language);
}
foreach (var culture in new[] { "it-IT", "it-CH", "en-US", "fr-FR" })
{
    var system = System.Globalization.CultureInfo.GetCultureInfo(culture);
    Equal(culture.StartsWith("it") ? "it" : "en", Localizer.Resolve("auto", system), "System language " + culture);
    Equal("en", Localizer.Resolve("en", system), "Explicit English " + culture);
    Equal("it", Localizer.Resolve("it", system), "Explicit Italian " + culture);
}
var en = Localizer.Resources.GetResourceSet(System.Globalization.CultureInfo.GetCultureInfo("en"), true, true)!;
var it = Localizer.Resources.GetResourceSet(System.Globalization.CultureInfo.GetCultureInfo("it"), true, false)!;
var enKeys = en.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
var itKeys = it.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Order().ToArray();
Equal(string.Join(",", enKeys), string.Join(",", itKeys), "Italian resource keys exactly match English");
Equal(string.Join(",", enKeys), string.Join(",", typeof(TextKeys).GetFields().Select(f => (string)f.GetRawConstantValue()!).Order()), "Typed keys cover all resources");
foreach (var key in enKeys)
{
    var english = (string)en.GetObject(key)!; var italian = (string)it.GetObject(key)!;
    Equal(true, !string.IsNullOrWhiteSpace(italian), "Italian text exists for " + key);
    var pattern = @"\{[^}]+\}";
    Equal(string.Join(",", System.Text.RegularExpressions.Regex.Matches(english, pattern).Select(m => m.Value)),
        string.Join(",", System.Text.RegularExpressions.Regex.Matches(italian, pattern).Select(m => m.Value)), "Placeholder parity " + key);
}
Equal("Play", Localizer.Resources.GetString(TextKeys.Play, System.Globalization.CultureInfo.GetCultureInfo("fr-FR")), "Unsupported resource culture falls back to English");
Equal("Riproduci", Localizer.Resources.GetString(TextKeys.Play, System.Globalization.CultureInfo.GetCultureInfo("it-CH")), "Italian regional fallback uses satellite");
var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
Localizer.Current.Apply("it"); Equal("Riproduci", Localizer.Get(TextKeys.Play), "Italian resources resolve");
Equal(originalCulture, System.Globalization.CultureInfo.CurrentCulture, "Numeric culture remains unchanged");
Localizer.Current.Apply("en"); Equal("Play", Localizer.Get(TextKeys.Play), "English resources resolve");
// A directory at the destination simulates a non-writable settings file without changing ACLs.
var blocked = Path.Combine(directory, Guid.NewGuid().ToString()); Directory.CreateDirectory(blocked);
Equal(false, new SettingsStore(blocked).Save(loaded), "Unwritable destination reports failure without throwing");
using (var held = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    Equal(false, store.Save(loaded), "Locked settings file reports failure without throwing");
Console.WriteLine($"{count} logic checks passed.");
