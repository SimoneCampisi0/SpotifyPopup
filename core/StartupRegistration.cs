using Microsoft.Win32;

namespace SpotifyPopup;

internal sealed class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpotifyPopup";
    internal string Command => "\"" + Environment.ProcessPath + "\" --autostart";
    internal bool Matches(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        var value = key?.GetValue(ValueName) as string;
        return enabled ? value == Command : value == null;
    }
    internal bool TryApply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
            if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
            else key.DeleteValue(ValueName, false);
            return Matches(enabled);
        }
        catch (Exception ex) { Diagnostics.Record("startup-registration", ex); return false; }
    }
}
