using System.Windows.Input;
using System.Windows.Interop;

namespace SpotifyPopup;

internal sealed class HotkeyManager : IDisposable
{
    private const int FirstId = 0x5031;
    private readonly HwndSource source;
    private string[] current = Array.Empty<string>();
    private readonly Action[] actions;
    private bool suspended;
    internal void Suspend() { Unregister(); suspended = true; }
    internal HotkeyManager(HwndSource source, Action toggle, Action unlock, Action? play = null,
        Action? previous = null, Action? next = null, Action? backward = null, Action? forward = null)
    {
        this.source = source; actions = new[] { toggle, unlock, play ?? (() => { }), previous ?? (() => { }), next ?? (() => { }), backward ?? (() => { }), forward ?? (() => { }) };
        source.AddHook(Hook);
    }
    internal bool TryApply(string toggle, string unlock, out string error) => TryApply(new AppSettings { ToggleShortcut = toggle, UnlockShortcut = unlock }, out error);
    internal bool TryApply(AppSettings settings, out string error)
    {
        var values = settings.Shortcuts;
        var parsed = new List<(uint Modifiers, uint Key)>();
        for (var i = 0; i < values.Length; i++)
        {
            if (i >= 2 && string.IsNullOrWhiteSpace(values[i])) { parsed.Add((0, 0)); continue; }
            if (!Parse(values[i], out var modifiers, out var key)) { error = TextKeys.ShortcutInvalid; return false; }
            if (parsed.Contains((modifiers, key))) { error = TextKeys.ShortcutDuplicate; return false; }
            parsed.Add((modifiers, key));
        }
        if (!suspended && values.SequenceEqual(current)) { error = ""; return true; }
        var old = current; var wasSuspended = suspended;
        Unregister();
        for (var i = 0; i < parsed.Count; i++)
        {
            var (modifiers, key) = parsed[i];
            if (key == 0 || NativeMethods.RegisterHotKey(source.Handle, FirstId + i, modifiers | 0x4000, key)) continue;
            Unregister();
            var restored = true;
            for (var j = 0; !wasSuspended && j < old.Length; j++)
                if (Parse(old[j], out var om, out var ok)) restored &= NativeMethods.RegisterHotKey(source.Handle, FirstId + j, om | 0x4000, ok);
            current = restored ? old : Array.Empty<string>(); suspended = wasSuspended;
            error = TextKeys.ShortcutBusy; return false;
        }
        current = values; suspended = false; error = ""; return true;
    }
    internal static bool Parse(string text, out uint modifiers, out uint key)
    {
        modifiers = key = 0;
        try
        {
            var gesture = (KeyGesture?)new KeyGestureConverter().ConvertFromInvariantString(text.Replace("Win+", "Windows+", StringComparison.OrdinalIgnoreCase));
            if (gesture == null || gesture.Key == Key.None || gesture.Modifiers == ModifierKeys.None) return false;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 1;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 2;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 4;
            if (gesture.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 8;
            key = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key); return key != 0;
        }
        catch (Exception ex) when (ex is NotSupportedException or FormatException or ArgumentException) { return false; }
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var index = (int)wParam - FirstId;
        if (message == 0x0312 && index >= 0 && index < actions.Length) { actions[index](); handled = true; }
        return IntPtr.Zero;
    }
    private void Unregister() { for (var i = 0; i < actions.Length; i++) NativeMethods.UnregisterHotKey(source.Handle, FirstId + i); }
    public void Dispose() { Unregister(); source.RemoveHook(Hook); }
}
