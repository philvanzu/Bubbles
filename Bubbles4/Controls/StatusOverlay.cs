using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Bubbles4.Models;

namespace Bubbles4.Controls;

public class StatusOverlay : Control
{
    public static readonly StyledProperty<string?> PagingStatusProperty =
        AvaloniaProperty.Register<StatusOverlay, string?>(nameof(PagingStatus));

    public string? PagingStatus
    {
        get => GetValue(PagingStatusProperty);
        set => SetValue(PagingStatusProperty, value);
    }

    public static readonly StyledProperty<string?> BookStatusProperty =
        AvaloniaProperty.Register<StatusOverlay, string?>(nameof(BookStatus));

    public string? BookStatus
    {
        get => GetValue(BookStatusProperty);
        set => SetValue(BookStatusProperty, value);
    }

    public static readonly StyledProperty<string?> PageStatusProperty =
        AvaloniaProperty.Register<StatusOverlay, string?>(nameof(PageStatus));

    public string? PageStatus
    {
        get => GetValue(PageStatusProperty);
        set => SetValue(PageStatusProperty, value);
    }

    
    
    public static readonly StyledProperty<bool> IsFullscreenProperty =
        AvaloniaProperty.Register<StatusOverlay, bool>(nameof(IsFullscreen));

    public bool IsFullscreen
    {
        get => GetValue(IsFullscreenProperty);
        set => SetValue(IsFullscreenProperty, value);
    }

    
    private double _pagingOpacity; // 0 = invisible, 1 = fully visible
    private double _bookOpacity;
    private double _pageOpacity;
    private UserSettings _prefs;
    Point[] _offsets = new[]
    {
        new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
        new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0)
    };
    Typeface typeface = new Typeface("Consolas");
    int fontSize = 16;
    CultureInfo culture = CultureInfo.CurrentUICulture;
    FlowDirection flow = FlowDirection.LeftToRight;


    private DateTime? _pagingTime;
    private DateTime? _pageTime;
    private DateTime? _bookTime;
    
    const float fadeTime = 1.5f;
    private readonly DispatcherTimer animTimer;

    
    
    public StatusOverlay()
    {

        Focusable = false;
        IsHitTestVisible = false;
        animTimer = new DispatcherTimer();
        animTimer.Tick += OnAnimTick;
        animTimer.Interval = TimeSpan.FromMilliseconds(10);
        animTimer.Start();
        _prefs = AppStorage.Instance.UserSettings;

    }

    
    private class FadeField
    {
        public required Func<int> GetDisplayTime;
        public required Func<DateTime?> GetStartTime;
        public required Action<DateTime?> SetStartTime;
        public required Action<double> SetOpacity;
    }
    private void OnAnimTick(object? sender, EventArgs e)
    {
        
        bool invalidateVisual = false;

        var now = DateTime.Now;

        var fadeFields = new[]
        {
            new FadeField
            {
                GetDisplayTime = () => _prefs.ShowPagingInfo,
                GetStartTime = () => _pagingTime,
                SetStartTime = t => _pagingTime = t,
                SetOpacity = o => _pagingOpacity = o
            },
            new FadeField
            {
                GetDisplayTime = () => _prefs.ShowPageName,
                GetStartTime = () => _pageTime,
                SetStartTime = t => _pageTime = t,
                SetOpacity = o => _pageOpacity = o
            },
            new FadeField
            {
                GetDisplayTime = () => _prefs.ShowAlbumPath,
                GetStartTime = () => _bookTime,
                SetStartTime = t => _bookTime = t,
                SetOpacity = o => _bookOpacity = o
            },
        };

        foreach (var field in fadeFields)
        {
            var startTime = field.GetStartTime();
            if (startTime is null) continue;

            double elapsed = (now - startTime.Value).TotalSeconds;
            int displayTime = field.GetDisplayTime();

            if (elapsed > displayTime)
            {
                elapsed -= displayTime;
                if (elapsed >= fadeTime)
                {
                    field.SetStartTime(null);
                    field.SetOpacity(0);
                }
                else
                {
                    double t = Math.Clamp(elapsed / fadeTime, 0, 1);
                    field.SetOpacity(Lerp(1, 0, t));
                }
                invalidateVisual = true;
            }
        }

        if (invalidateVisual) InvalidateVisual();
    }

    public void ShowAll(bool show)
    {
        if (show)
        {
            _pagingTime = DateTime.Now;
            _pagingOpacity = 1;
            _bookTime = DateTime.Now;
            _bookOpacity = 1;
            _pageTime = DateTime.Now;
            _pageOpacity = 1;
        }
        else
        {
            PagingStatus = PagingStatus;
            BookStatus = BookStatus;
            PageStatus = PageStatus;
        }
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        switch (change.Property.Name)
        {
            case nameof(PagingStatus) :
                
                _pagingTime = null;
                
                if (_prefs.ShowPagingInfo > 0)
                {
                    _pagingTime = DateTime.Now;
                    _pagingOpacity = 1;
                }
                else if (_prefs.ShowPagingInfo == 0) _pagingOpacity = 1; //doesn't fade
                else _pagingOpacity = 0; // never show
                
                InvalidateVisual();
                break;
            
            case nameof(BookStatus):
                _prefs = AppStorage.Instance.UserSettings;
                _bookTime = null;
                
                if (_prefs.ShowAlbumPath > 0)
                {
                    _bookTime = DateTime.Now;
                    _bookOpacity = 1;
                }
                else if (_prefs.ShowAlbumPath == 0) _bookOpacity = 1; //doesn't fade
                else _bookOpacity = 0; // never show
                
                InvalidateVisual();
                break;
            
            case nameof(PageStatus):
                _pageTime = null;
                
                if (_prefs.ShowPageName > 0)
                {
                    _pageTime = DateTime.Now;
                    _pageOpacity = 1;
                }
                else if (_prefs.ShowPageName == 0) _pageOpacity = 1; //doesn't fade
                else _pageOpacity = 0; // never show
                
                InvalidateVisual();
                break;

            case nameof(_prefs):
                //_prefs is swapped when toggling fullscreen
                //non fullscreen version never displays anything
                PagingStatus = PagingStatus;
                PageStatus = PageStatus;
                BookStatus = BookStatus;
                break;

        } 
        
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsFullscreen)
        {
            var bounds = Bounds;
            Tuple<string?, Point, double>[] arr = new[]
            {
                new Tuple<string?, Point, double>(PagingStatus, new Point(bounds.Width - 8, 8), _pagingOpacity),
                new Tuple<string?, Point, double>(BookStatus, new Point(8, 8), _bookOpacity),
                new Tuple<string?, Point, double>(PageStatus, new Point(8, bounds.Height - 24), _pageOpacity),
            };
        
            int i = 0;
        
            foreach (var tuple in arr)
            {
                var text = tuple.Item1;
                if (string.IsNullOrEmpty(text)) continue;
                var pos = tuple.Item2;
                var opacity = tuple.Item3;
            
                if (!string.IsNullOrEmpty(text))
                {
                    var formattedText = new FormattedText(text, culture, flow, typeface, fontSize, Brushes.White);
                    if (i==0) pos = new Point(pos.X - formattedText.Width, pos.Y);
                
                    using (context.PushOpacity(opacity))
                    {
                        var outlineText = new FormattedText(text, culture, flow, typeface, fontSize, Brushes.Black);

                        // Draw outline in black
                        foreach (var offset in _offsets)
                            context.DrawText(outlineText, pos + offset);

                        // Draw main white text
                        context.DrawText(formattedText, pos);
                    }
                }

                i++;
            }    
        }
    }
    
    double Lerp(double from, double to, double t)
    {
        return from + (to - from) * t;
    }

}