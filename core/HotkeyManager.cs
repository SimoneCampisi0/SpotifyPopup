using System.Windows.Input;
using System.Windows.Interop;

namespace SpotifyPopup;

internal sealed class HotkeyManager : IDisposable
{
    private const int ToggleId = 0x5031, UnlockId = 0x5032;
    private readonly HwndSource source;
    private string toggle = "", unlock = "";
    private readonly Action toggleAction, unlockAction;
    internal HotkeyManager(HwndSource source, Action toggleAction, Action unlockAction)
    { this.source = source; this.toggleAction = toggleAction; this.unlockAction = unlockAction; source.AddHook(Hook); }

    internal bool TryApply(string toggleValue, string unlockValue, out string error)
    {
        if (!Parse(toggleValue, out var tm, out var tk) || !Parse(unlockValue, out var um, out var uk))
        { error = "Choose a key with Ctrl, Alt, Shift or Win (for example Ctrl+Alt+P)."; return false; }
        if (tm == um && tk == uk) { error = "Show/hide and unlock must use different shortcuts."; return false; }
        if (toggleValue == toggle && unlockValue == unlock) { error = ""; return true; }
        var oldToggle = toggle; var oldUnlock = unlock;
        Unregister();
        bool first = NativeMethods.RegisterHotKey(source.Handle, ToggleId, tm | 0x4000, tk);
        bool second = first && NativeMethods.RegisterHotKey(source.Handle, UnlockId, um | 0x4000, uk);
        if (first && second) { toggle = toggleValue; unlock = unlockValue; error = ""; return true; }
        Unregister();
        if (Parse(oldToggle, out tm, out tk)) NativeMethods.RegisterHotKey(source.Handle, ToggleId, tm | 0x4000, tk);
        if (Parse(oldUnlock, out um, out uk)) NativeMethods.RegisterHotKey(source.Handle, UnlockId, um | 0x4000, uk);
        toggle = oldToggle; unlock = oldUnlock;
        error = "A shortcut is already in use. Choose another combination."; return false;
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
        if (message != 0x0312) return IntPtr.Zero;
        if ((int)wParam == ToggleId) { toggleAction(); handled = true; }
        else if ((int)wParam == UnlockId) { unlockAction(); handled = true; }
        return IntPtr.Zero;
    }
    private void Unregister() { NativeMethods.UnregisterHotKey(source.Handle, ToggleId); NativeMethods.UnregisterHotKey(source.Handle, UnlockId); }
    public void Dispose() { Unregister(); source.RemoveHook(Hook); }
}
