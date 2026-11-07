using System.ComponentModel;
using System.Windows.Media;

namespace SpotifyPopup;

public enum MediaAction { PlayPause, Previous, Next, Seek }

public sealed record MediaSnapshot
{
    public string Title { get; init; } = "";
    public string TitleFallbackKey { get; init; } = TextKeys.OpenSpotifyPrompt;
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string Status { get; init; } = TextKeys.SpotifyUnavailable;
    public ImageSource? Artwork { get; init; }
    public bool Connected { get; init; }
    public bool Playing { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanNext { get; init; }
    public bool CanSeek { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public string TrackKey { get; init; } = "";
}

public interface IMediaController : IDisposable
{
    MediaSnapshot Snapshot { get; }
    event EventHandler<MediaSnapshot>? Updated;
    event EventHandler<string>? Notice;
    Task InitializeAsync();
    void Tick();
    Task ExecuteAsync(MediaAction action, double fraction = 0, string? expectedTrackKey = null);
}

public sealed class PlayerState : INotifyPropertyChanged, IDisposable
{
    private MediaSnapshot snapshot = new();
    private bool working;
    private string notice = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public PlayerState() => Localizer.Current.PropertyChanged += LanguageChanged;
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => Changed();
    public void Dispose() => Localizer.Current.PropertyChanged -= LanguageChanged;
    public string Title => string.IsNullOrWhiteSpace(snapshot.Title) ? Localizer.Get(snapshot.TitleFallbackKey) : snapshot.Title;
    public string Artist => snapshot.Artist;
    public string Album => snapshot.Album;
    public string Status => Localizer.Get(snapshot.Status);
    public ImageSource? Artwork => snapshot.Artwork;
    public bool Connected => snapshot.Connected;
    public bool Playing => snapshot.Playing;
    public bool CanPlayPause => snapshot.CanPlayPause && !working;
    public bool CanPrevious => snapshot.CanPrevious && !working;
    public bool CanNext => snapshot.CanNext && !working;
    public bool CanSeek => snapshot.CanSeek && !working;
    public string PlayGlyph => Playing ? "\uE769" : "\uE768";
    public string PlayLabel => Localizer.Get(Playing ? TextKeys.Pause : TextKeys.Play);
    public string Elapsed => PlaybackMath.Format(snapshot.Position);
    public string Duration => PlaybackMath.Format(snapshot.Duration);
    public string Notice => Localizer.Get(notice);
    public void Apply(MediaSnapshot value) { snapshot = value; Changed(); }
    public void SetWorking(bool value) { working = value; Changed(); }
    public void SetNotice(string value) { notice = value; Changed(); }
    private void Changed() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
