namespace SpotifyPopup;

// Manual visibility wins until playback or connection changes, or preferences are saved.
public sealed class AutoVisibilityPolicy
{
    private bool? manual;
    private bool initialized, connected, playing;
    private DateTimeOffset since;
    public void Manual(bool visible) => manual = visible;
    public void Reset() { initialized = false; manual = null; }
    public bool? Evaluate(bool isConnected, bool isPlaying, DateTimeOffset now, AppSettings settings)
    {
        if (!initialized) { initialized = true; connected = isConnected; playing = isPlaying; since = now; }
        else if (connected != isConnected || playing != isPlaying)
        { connected = isConnected; playing = isPlaying; since = now; manual = null; }
        if (manual.HasValue) return null;
        if (!settings.HideWhenDisconnected && !settings.HideWhenPaused) return null;
        if (!connected && settings.HideWhenDisconnected)
            return now - since >= TimeSpan.FromSeconds(5) ? false : null;
        if (connected && !playing && settings.HideWhenPaused)
            return now - since >= TimeSpan.FromSeconds(settings.PauseDelaySeconds) ? false : null;
        return connected && playing ? true : null;
    }
}
