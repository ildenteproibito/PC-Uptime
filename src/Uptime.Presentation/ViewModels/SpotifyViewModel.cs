using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class SpotifyViewModel : ObservableObject, IDisposable
{
    private readonly ISpotifyMediaService _media;
    private readonly DispatcherTimer _secondTimer;
    private TimeSpan _currentPosition = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;

    public Localizer L => Localizer.Instance;

    [ObservableProperty] private bool _hasTrack;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private string _album = "";
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isLooping;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 100;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private Bitmap? _coverImage;

    public SpotifyViewModel(ISpotifyMediaService media)
    {
        _media = media;
        _media.TrackChanged += OnTrackChanged;
        Localizer.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Localizer.Language)) OnPropertyChanged(nameof(L));
        };

        _secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _secondTimer.Tick += OnSecondTick;

        UpdateFrom(_media.CurrentTrack);
    }

    private void OnSecondTick(object? sender, EventArgs e)
    {
        if (!HasTrack || !IsPlaying)
        {
            _secondTimer.Stop();
            return;
        }

        _currentPosition += TimeSpan.FromSeconds(1);
        if (_duration > TimeSpan.Zero && _currentPosition > _duration)
        {
            _currentPosition = _duration;
        }

        ProgressValue = _currentPosition.TotalSeconds;
        PositionText = FormatTime(_currentPosition);
    }

    private void OnTrackChanged(MediaTrackInfo info)
    {
        Dispatcher.UIThread.Post(() => UpdateFrom(info));
    }

    private void UpdateFrom(MediaTrackInfo info)
    {
        var trackChanged = Title != info.Title || Artist != info.Artist;

        HasTrack = info.HasTrack;
        Title = string.IsNullOrWhiteSpace(info.Title) ? L["SpotifyNoTrack"] : info.Title;
        Artist = info.Artist;
        Album = info.Album;
        IsPlaying = info.IsPlaying;
        IsLooping = info.IsLooping;

        _duration = info.Duration;
        ProgressMax = Math.Max(1, _duration.TotalSeconds);
        DurationText = FormatTime(_duration);

        if (trackChanged || !info.IsPlaying)
        {
            _currentPosition = info.Position;
        }
        else
        {
            // Only update position on significant delta (e.g. external seek > 2.5s) to prevent micro-jitter during playback
            var diff = Math.Abs((info.Position - _currentPosition).TotalSeconds);
            if (diff > 2.5)
            {
                _currentPosition = info.Position;
            }
        }

        ProgressValue = _currentPosition.TotalSeconds;
        PositionText = FormatTime(_currentPosition);

        if (HasTrack && IsPlaying)
        {
            if (!_secondTimer.IsEnabled) _secondTimer.Start();
        }
        else
        {
            if (_secondTimer.IsEnabled) _secondTimer.Stop();
        }

        if (info.ThumbnailBytes is { Length: > 0 } bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                CoverImage = new Bitmap(ms);
            }
            catch
            {
                CoverImage = null;
            }
        }
        else
        {
            CoverImage = null;
        }
    }

    private static string FormatTime(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return $"{(int)t.TotalMinutes}:{t.Seconds:D2}";
    }

    [RelayCommand]
    private async Task TogglePlayPause() => await _media.TogglePlayPauseAsync();

    [RelayCommand]
    private async Task SkipNext() => await _media.SkipNextAsync();

    [RelayCommand]
    private async Task SkipPrevious() => await _media.SkipPreviousAsync();

    [RelayCommand]
    private async Task SeekBackward10()
    {
        _currentPosition = _currentPosition > TimeSpan.FromSeconds(10) ? _currentPosition - TimeSpan.FromSeconds(10) : TimeSpan.Zero;
        ProgressValue = _currentPosition.TotalSeconds;
        PositionText = FormatTime(_currentPosition);
        await _media.SeekBySecondsAsync(-10);
    }

    [RelayCommand]
    private async Task SeekForward10()
    {
        _currentPosition += TimeSpan.FromSeconds(10);
        if (_duration > TimeSpan.Zero && _currentPosition > _duration) _currentPosition = _duration;
        ProgressValue = _currentPosition.TotalSeconds;
        PositionText = FormatTime(_currentPosition);
        await _media.SeekBySecondsAsync(10);
    }

    [RelayCommand]
    private async Task ToggleLoop() => await _media.ToggleLoopAsync();

    public void Dispose()
    {
        _secondTimer.Stop();
        _secondTimer.Tick -= OnSecondTick;
        _media.TrackChanged -= OnTrackChanged;
        CoverImage?.Dispose();
    }
}
