using System.Diagnostics;
using System.IO;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsMediaService : ISpotifyMediaService, IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public event Action<MediaTrackInfo>? TrackChanged;
    public MediaTrackInfo CurrentTrack { get; private set; } = new(false, "", "", "", false, "", null);

    public WindowsMediaService()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged += OnCurrentSessionChanged;
                _manager.SessionsChanged += OnSessionsChanged;
                await UpdateSessionAndTrackAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Windows SMTC init warning: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = UpdateSessionAndTrackAsync();
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = UpdateSessionAndTrackAsync();
    }

    private async Task UpdateSessionAndTrackAsync()
    {
        if (_disposed || _manager == null) return;
        await _gate.WaitAsync();
        try
        {
            var sessions = _manager.GetSessions();
            var spotifySession = sessions.FirstOrDefault(s =>
                !string.IsNullOrEmpty(s.SourceAppUserModelId) &&
                s.SourceAppUserModelId.Contains("spotify", StringComparison.OrdinalIgnoreCase));

            var targetSession = spotifySession ?? _manager.GetCurrentSession();

            if (_currentSession != null && !ReferenceEquals(_currentSession, targetSession))
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }

            _currentSession = targetSession;

            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }

            await FetchPropertiesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateSessionAndTrack error: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = FetchPropertiesAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = FetchPropertiesAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = FetchPropertiesAsync(quiet: true);
    }

    private async Task FetchPropertiesAsync(bool quiet = false)
    {
        if (_currentSession == null)
        {
            CurrentTrack = new MediaTrackInfo(false, "", "", "", false, "", null);
            TrackChanged?.Invoke(CurrentTrack);
            return;
        }

        try
        {
            var props = await _currentSession.TryGetMediaPropertiesAsync();
            var playback = _currentSession.GetPlaybackInfo();
            var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var isLooping = playback?.AutoRepeatMode is MediaPlaybackAutoRepeatMode.Track or MediaPlaybackAutoRepeatMode.List;

            var timeline = _currentSession.GetTimelineProperties();
            var position = timeline?.Position ?? TimeSpan.Zero;
            var duration = timeline?.EndTime ?? TimeSpan.Zero;

            if (isPlaying && timeline != null && timeline.LastUpdatedTime > DateTimeOffset.MinValue)
            {
                var delta = DateTimeOffset.UtcNow - timeline.LastUpdatedTime.ToUniversalTime();
                if (delta > TimeSpan.Zero && delta < TimeSpan.FromHours(4))
                {
                    position += delta;
                    if (duration > TimeSpan.Zero && position > duration)
                    {
                        position = duration;
                    }
                }
            }

            if (props == null || (string.IsNullOrWhiteSpace(props.Title) && string.IsNullOrWhiteSpace(props.Artist)))
            {
                CurrentTrack = new MediaTrackInfo(false, "", "", "", isPlaying, _currentSession.SourceAppUserModelId ?? "", null, position, duration, isLooping);
            }
            else
            {
                byte[]? thumb = CurrentTrack.ThumbnailBytes;
                if (!quiet || thumb == null)
                {
                    if (props.Thumbnail != null)
                    {
                        try
                        {
                            using var streamRef = await props.Thumbnail.OpenReadAsync();
                            using var netStream = streamRef.AsStreamForRead();
                            using var ms = new MemoryStream();
                            await netStream.CopyToAsync(ms);
                            thumb = ms.ToArray();
                        }
                        catch
                        {
                            // Fallback if cover thumbnail retrieval fails
                        }
                    }
                }

                CurrentTrack = new MediaTrackInfo(
                    true,
                    props.Title ?? "",
                    props.Artist ?? "",
                    props.AlbumTitle ?? "",
                    isPlaying,
                    _currentSession.SourceAppUserModelId ?? "",
                    thumb,
                    position,
                    duration,
                    isLooping);
            }

            TrackChanged?.Invoke(CurrentTrack);
        }
        catch
        {
            CurrentTrack = new MediaTrackInfo(false, "", "", "", false, "", null);
            TrackChanged?.Invoke(CurrentTrack);
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession != null)
        {
            try { await _currentSession.TryTogglePlayPauseAsync(); }
            catch (Exception ex) { Debug.WriteLine($"TogglePlayPause error: {ex.Message}"); }
        }
    }

    public async Task SkipNextAsync()
    {
        if (_currentSession != null)
        {
            try { await _currentSession.TrySkipNextAsync(); }
            catch (Exception ex) { Debug.WriteLine($"SkipNext error: {ex.Message}"); }
        }
    }

    public async Task SkipPreviousAsync()
    {
        if (_currentSession != null)
        {
            try { await _currentSession.TrySkipPreviousAsync(); }
            catch (Exception ex) { Debug.WriteLine($"SkipPrevious error: {ex.Message}"); }
        }
    }

    public async Task SeekBySecondsAsync(int seconds)
    {
        if (_currentSession != null)
        {
            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                var pos = timeline?.Position ?? TimeSpan.Zero;
                var playback = _currentSession.GetPlaybackInfo();
                var isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (isPlaying && timeline != null && timeline.LastUpdatedTime > DateTimeOffset.MinValue)
                {
                    var delta = DateTimeOffset.UtcNow - timeline.LastUpdatedTime.ToUniversalTime();
                    if (delta > TimeSpan.Zero && delta < TimeSpan.FromHours(4)) pos += delta;
                }
                var end = timeline?.EndTime ?? TimeSpan.FromHours(1);
                var targetSec = Math.Clamp(pos.TotalSeconds + seconds, 0, Math.Max(0, end.TotalSeconds));
                var ticks = (long)(targetSec * TimeSpan.TicksPerSecond);
                await _currentSession.TryChangePlaybackPositionAsync(ticks);
                await FetchPropertiesAsync(quiet: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SeekBySeconds error: {ex.Message}");
            }
        }
    }

    public async Task ToggleLoopAsync()
    {
        if (_currentSession != null)
        {
            try
            {
                var playback = _currentSession.GetPlaybackInfo();
                var currentMode = playback?.AutoRepeatMode ?? MediaPlaybackAutoRepeatMode.None;
                var targetMode = currentMode == MediaPlaybackAutoRepeatMode.None
                    ? MediaPlaybackAutoRepeatMode.Track
                    : MediaPlaybackAutoRepeatMode.None;
                await _currentSession.TryChangeAutoRepeatModeAsync(targetMode);
                await FetchPropertiesAsync(quiet: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ToggleLoop error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _currentSession = null;
        }

        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager = null;
        }

        _gate.Dispose();
    }
}
