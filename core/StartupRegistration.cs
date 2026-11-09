using Microsoft.Win32;

namespace SpotifyPopup;

internal interface IStartupEntry
{
    string? Read();
    void Write(string? value);
}
internal sealed class RegistryStartupEntry : IStartupEntry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpotifyPopup";
    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }
    public void Write(string? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        if (value == null) key.DeleteValue(ValueName, false);
        else key.SetValue(ValueName, value, RegistryValueKind.String);
    }
}
internal sealed class StartupRegistration
{
    private readonly IStartupEntry entry;
    internal string Command { get; }
    internal StartupRegistration(IStartupEntry? entry = null, string? executable = null)
    {
        this.entry = entry ?? new RegistryStartupEntry();
        Command = "\"" + (executable ?? Environment.ProcessPath) + "\" --autostart";
    }
    internal bool Matches(bool enabled) => enabled ? entry.Read() == Command : entry.Read() == null;
    internal bool TryApply(bool enabled)
    {
        string? previous = null; var read = false;
        try
        {
            previous = entry.Read(); read = true;
            entry.Write(enabled ? Command : null);
            if (!Matches(enabled)) throw new IOException();
            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Record("startup-registration", ex);
            if (read) try { entry.Write(previous); } catch (Exception rollback) { Diagnostics.Record("startup-rollback", rollback); }
            return false;
        }
    }
}
