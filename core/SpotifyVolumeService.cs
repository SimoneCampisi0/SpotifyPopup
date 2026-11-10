using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SpotifyPopup;

// Core Audio session volume, never the device-wide master volume.
// Windows can expose several sessions for the same Spotify desktop process.
internal static class SpotifyVolumeService
{
    internal static float? Read() => WithSessions(null);
    internal static bool Write(float volume) => WithSessions(Math.Clamp(volume, 0f, 1f)).HasValue;

    private static float? WithSessions(float? requested)
    {
        IMMDeviceEnumerator? devices = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            devices = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            devices.GetDefaultAudioEndpoint(0, 1, out device);
            var iid = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out var raw);
            try { manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(raw); }
            finally { Marshal.Release(raw); }
            manager.GetSessionEnumerator(out sessions);
            sessions.GetCount(out var count);
            var found = 0;
            var aggregate = 0f;
            for (var i = 0; i < count; i++)
            {
                IAudioSessionControl? control = null;
                try
                {
                    sessions.GetSession(i, out control);
                    if (control is not IAudioSessionControl2 advanced || control is not ISimpleAudioVolume audio) continue;
                    advanced.GetProcessId(out var processId);
                    if (!IsSpotifyProcess(processId)) continue;
                    if (requested.HasValue)
                        audio.SetMasterVolume(requested.Value, IntPtr.Zero);
                    audio.GetMasterVolume(out var current);
                    aggregate += current;
                    found++;
                }
                catch (COMException ex) { Diagnostics.Record("volume-session", ex); }
                finally { Release(control); }
            }
            return found == 0 ? null : aggregate / found;
        }
        catch (COMException ex) { Diagnostics.Record("volume-access", ex); return null; }
        catch (InvalidCastException ex) { Diagnostics.Record("volume-interface", ex); return null; }
        finally { Release(sessions); Release(manager); Release(device); Release(devices); }
    }
    private static bool IsSpotifyProcess(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)pid));
            return process.ProcessName.Equals("Spotify", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
        catch (OverflowException) { return false; }
    }
    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int flow, int mask, out IntPtr devices);
        void GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr callback);
        void UnregisterEndpointNotificationCallback(IntPtr callback);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, uint context, IntPtr parameters, out IntPtr result);
        void OpenPropertyStore(int access, out IntPtr properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out int state);
    }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void GetAudioSessionControl(ref Guid session, uint flags, out IntPtr control);
        void GetSimpleAudioVolume(ref Guid session, uint flags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        void RegisterSessionNotification(IntPtr notification);
        void UnregisterSessionNotification(IntPtr notification);
        void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);
        void UnregisterDuckNotification(IntPtr notification);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl control);
    }
    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr context);
        void GetGroupingParam(out Guid grouping);
        void SetGroupingParam(ref Guid grouping, IntPtr context);
        void RegisterAudioSessionNotification(IntPtr notifications);
        void UnregisterAudioSessionNotification(IntPtr notifications);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr context);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr context);
        void GetGroupingParam(out Guid grouping);
        void SetGroupingParam(ref Guid grouping, IntPtr context);
        void RegisterAudioSessionNotification(IntPtr notifications);
        void UnregisterAudioSessionNotification(IntPtr notifications);
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out uint pid);
        void IsSystemSoundsSession();
        void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        void SetMasterVolume(float volume, IntPtr context);
        void GetMasterVolume(out float volume);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
