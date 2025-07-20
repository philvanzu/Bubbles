using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;
using Avalonia.Threading;
using Bubbles4.Models;
using Bubbles4.Services;
using Bubbles4.ViewModels;
using Bubbles4.Views;

namespace Bubbles4.Controls {
    public class FastImageViewer : Control
    {
        private enum Edge { Top, Bottom, Left, Right }
        
        private Point _panOffset = new(0, 0);
        private double _zoom = 1.0;
        private double _minZoom = 1.0;
        private double _fitZoom = 1.0;
        private double _fitWZoom = 1.0;
        private double _fitHZoom = 1.0;
        private const double _maxZoom = 10.0;
        private Bitmap? _image;
        private Point _lastPointerPosition;
        private Size _lastViewportSize = new Size(0, 0);
        private PageViewModel? _page;  
        private BookViewModel? _previousBook;
        private IvpRect? _ivpRect;
        private IvpAnimation? _ivpAnim;
        private bool _fullscreenToggled;
        private readonly DispatcherTimer turnPageTimer;
        private bool _topHit, _bottomHit, _turnPageOnScrollUp, _turnPageOnScrollDown, _noScrolling;

        public static readonly StyledProperty<MainViewModel?> MainViewModelProperty =
            AvaloniaProperty.Register<FastImageViewer, MainViewModel?>(nameof(MainViewModel));
        public MainViewModel? MainViewModel
        {
            get => GetValue(MainViewModelProperty);
            set => SetValue(MainViewModelProperty, value);
        }
        
        public static readonly StyledProperty<ViewerData?> DataProperty =
            AvaloniaProperty.Register<FastImageViewer, ViewerData?>(nameof(Data));
        public ViewerData? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
        public static readonly StyledProperty<LibraryConfig?> ConfigProperty =
            AvaloniaProperty.Register<FastImageViewer, LibraryConfig?>(nameof(Config));
        public LibraryConfig? Config
        {
            get => GetValue(ConfigProperty);
            set => SetValue(ConfigProperty, value);
        }

        private bool _isFullscreen;

        
        bool UseIvp => _isFullscreen && Config != null && Config.UseIVPs;
        bool InScrollMode => _isFullscreen && Config?.ScrollAction == LibraryConfig.ScrollActions.Scroll;
        bool BookChanged => _page?.Book != _previousBook;
        private bool KeepZoom => _isFullscreen && Config?.LookAndFeel == LibraryConfig.LookAndFeels.Reader && !BookChanged; 
        public FastImageViewer()
        {
            Focusable = true;
            this.Focus();
            
            this.LayoutUpdated += OnLayoutUpdated;
            
            turnPageTimer = new DispatcherTimer();
            turnPageTimer.Tick += OnTurnPageTick;
            turnPageTimer.Interval = TimeSpan.FromMilliseconds(AppStorage.Instance.UserSettings.TurnPageBouncingTime);
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            if (VisualRoot is MainWindow mainWindow)
                mainWindow.ImgViewer = this;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            
            base.OnAttachedToVisualTree(e);
            bool fullscreen=_isFullscreen;
            Control? element = this;
            while (element != null)
            {
                if (element.Name == "FullscreenOverlay")
                {
                    fullscreen = true;
                    break;
                }
                else if (element.Name == "EmbeddedImageViewer")
                {
                    fullscreen = false;
                    break;
                }
                element = element.Parent as Control;
            }

            if (!_isFullscreen && fullscreen)
            {
                //Console.WriteLine("OnAttachedToVisualTree");
                OnEnteringFullscreen();
            }
            
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property.Name == nameof(MainViewModel))
            {
                if (MainViewModel != null) MainViewModel.ViewerControl = this;
            }
            if(change.Property.Name ==  nameof(Data)){

                    var data = change.NewValue as ViewerData;
                    //var oldData = change.OldValue as ViewerData;
                    
                    if (data?.Image == _image && data?.Page == _page)
                    {
                        //Console.WriteLine("data change was no change");
                        return;
                    }
                    if(!_fullscreenToggled)
                        SaveIvp();
                    
                    if (_ivpAnim?.IsRunning == true)
                    {
                        _ivpAnim.Stop(); 
                        _ivpAnim = null;
                    }
                    
                    if (BookChanged) _previousBook = _page?.Book;
                    
                    if (data is null || data.Image == null )
                    {
                        _image = null;
                        _page = null;
                        InvalidateVisual();
                        return;
                    }
                    
                    
                    bool isnext = true;
                    if (!BookChanged) isnext = data.Page.Index - _page?.Index > 0;
                    //Console.WriteLine($"isnext :{isnext}");
                    
                    _image = data.Image;
                    _page = data.Page;
                    _page.ImageSize = data.Image.PixelSize;
                    
                    if (_image != null)
                    {
                        AdjustZoomLimits();
                        if (Data != null)
                        {
                            if (_isFullscreen)
                            {
                                var ivp = _page?.Ivp;
                                if (ivp != null && UseIvp)
                                {
                                    if (Config?.AnimateIVPs == true)
                                    {
                                        AnimateIVP(ivp, Config.Fit);
                                    }
                                    else RestoreFromIVP(ivp);
                                }

                                else if (KeepZoom)
                                {
                                    ZoomTo(_zoom);
                                    if(isnext) PanTo(new Point(Bounds.Width / 2.0, 0));
                                    else PanTo(new Point(Bounds.Width / 2.0, -_image.PixelSize.Height * _zoom));
                                }
                                else
                                {
                                    switch (Config?.Fit)
                                    {
                                        case LibraryConfig.FitTypes.Height : FitHeight();
                                            break;
                                        case LibraryConfig.FitTypes.Width : FitWidth();
                                            break;
                                        case LibraryConfig.FitTypes.Stock : FitStock();
                                            break;
                                        default:
                                            Fit();
                                            break;
                                    }
                                }    
                            }
                            else Fit();
                        }
                    }
                    else
                    {
                        Console.WriteLine("_image is null");
                    }
                    InvalidateVisual();
            }

        }
        public void OnBookClosing()
        {
            SaveIvp();
            _page = null;
        }
        void SaveIvp()
        {
            if (_page != null && UseIvp)
            {
                //Console.WriteLine($"fstoggled: {_fullscreenToggled}, fs:{_isFullscreen}, bounds:{Bounds.Size}");
                var ivp = _ivpAnim?.IsRunning == true ? _ivpAnim.EndIvp : GetIVP();
                double defaultzoom = Config?.Fit switch
                {
                    LibraryConfig.FitTypes.Best => _fitZoom,
                    LibraryConfig.FitTypes.Height => _fitHZoom,
                    LibraryConfig.FitTypes.Width => _fitWZoom,
                    LibraryConfig.FitTypes.Stock => 1.0,
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (ivp!= null && Math.Abs(ivp.zoom - defaultzoom) < 0.0001) ivp = null;
                _page.Ivp = ivp;
            }
                
        }
        private void OnEnteringFullscreen()
        {
            //Console.WriteLine("setting fstoggled on");
            _fullscreenToggled = true;
        }
        
        public void OnExitingFullscreen()
        {
            SaveIvp();
            _fullscreenToggled = true;
        }
        private void OnLayoutUpdated(object? sender, EventArgs e)
        {

            if (_image != null && Bounds.Size != _lastViewportSize)
            {
                
                //Console.WriteLine($"LayoutUpdated last :{_lastViewportSize} || current:{Bounds.Size}, fs:{_isFullscreen}, toggled:{_fullscreenToggled}");
                AdjustZoomLimits();
                if (_fullscreenToggled || (_page != null && _page.Ivp != null && UseIvp))
                {
                    if (_fullscreenToggled)
                    {
                        _fullscreenToggled = false;
                        _isFullscreen = !_isFullscreen;
                    }
                    
                    //Console.WriteLine("fullscreen toggled");
                    if (!_isFullscreen)
                    {
//                        Console.WriteLine("fullscreen off => fitbest");
                        Fit();
                    }
                    else if (_page != null && _page.Ivp != null && UseIvp)
                    {
                        //Console.WriteLine("fullscreen on => ivp found, restoring it");
                        RestoreFromIVP(_page.Ivp);
                    }
                    else if (Config != null)
                    {
                        //Console.WriteLine("fullscreen on => config.Fit");
                        switch (Config?.Fit)
                        {
                            case LibraryConfig.FitTypes.Height:
                                
                                FitHeight();
                                break;
                            case LibraryConfig.FitTypes.Width:FitWidth();
                                FitWidth();
                                break;
                            case LibraryConfig.FitTypes.Stock:FitStock();
                                FitStock();
                                break;
                            default:
                                Fit();
                                break;
                        }
                    }
                    else
                    {
                        //Console.WriteLine("default fit : fitbest-");
                        Fit();
                    }
                }
                else
                {
                    //Console.WriteLine("Layout updated no toggle");
                    var newSize = Bounds.Size;

                    // Only apply proportional zoom change if both old and new sizes are valid
                    if (_lastViewportSize.Width > 0 && _lastViewportSize.Height > 0 &&
                        newSize.Width > 0 && newSize.Height > 0)
                    {
                        // Use height as the reference axis
                        double heightRatio = newSize.Height / _lastViewportSize.Height;
                        double widthRatio = newSize.Width / _lastViewportSize.Width;
                        var oldZoom = _zoom;
                        if (heightRatio < widthRatio) _zoom *= heightRatio;    
                        else _zoom *= widthRatio;                         
                        
                        _zoom = Math.Clamp(_zoom, _minZoom, _maxZoom);

                        // Maintain the position of the image center relative to viewport center
                        var centerBefore = new Point(_lastViewportSize.Width / 2, _lastViewportSize.Height / 2);
                        var imageCenterBefore = (centerBefore - _panOffset) / oldZoom;
                        var centerAfter = new Point(newSize.Width / 2, newSize.Height / 2);
                        _panOffset = centerAfter - imageCenterBefore * _zoom;
                    }
                }

                
                
                
                AdjustPanOffset();
                InvalidateVisual();
            }
            _lastViewportSize = Bounds.Size;
        }
        //Drawing
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_image == null)
                return;
            
            var imageSize = _image.Size;
            var destSize = new Size(imageSize.Width * _zoom, imageSize.Height * _zoom);
            var destRect = new Rect(_panOffset, destSize);
            var sourceRect = new Rect(imageSize);

            context.DrawImage(_image, sourceRect, destRect);
            if (InScrollMode)
                DrawVerticalScrollIndicator(context);

            if (_ivpRect != null && _ivpRect.IsValid)
            {
                var selectionRect = _ivpRect.ToRect();

                context.DrawRectangle(
                    brush: new SolidColorBrush(Color.FromArgb(64, 0, 120, 215)), // semi-transparent fill
                    pen: new Pen(Brushes.Blue),                                // solid blue border
                    rect: selectionRect
                );
            }
        }
        private void DrawVerticalScrollIndicator(DrawingContext context)
        {
            if (_image == null || Bounds.Height == 0)
                return;

            double ih = _image.PixelSize.Height * _zoom;
            double ch = Bounds.Height;

            // If image fits entirely, no scroll indicator needed
            if (ih <= ch) return;

            // Relative scroll progress (0.0 = top, 1.0 = bottom)
            double scrollProgress = -_panOffset.Y / (ih - ch);
            scrollProgress = Math.Clamp(scrollProgress, 0, 1);

            // Indicator visual parameters
            const double radius = 6;
            const double margin = 4;
            var cx = Bounds.Width - radius - margin;
            var cy = margin + scrollProgress * (ch - 2 * margin);

            var center = new Point(cx, cy);
            var brush = Brushes.White;
            if(_topHit || _bottomHit) brush = Brushes.Red;
            else if(_turnPageOnScrollDown || _turnPageOnScrollUp) brush = Brushes.Green;    
            var pen = new Pen(Brushes.Black);

            var rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            context.DrawGeometry(brush, pen, new EllipseGeometry(rect));
        }
        
        void AdjustZoomLimits()
        {
            if (_image == null || Bounds.Width == 0 || Bounds.Height == 0)
            {
                _zoom = 1.0;
                _minZoom = 1.0;
                return;
            } 
                            
            var iw = (double)_image.PixelSize.Width;   // image width
            var ih = (double)_image.PixelSize.Height;  // image height
            var cw = Bounds.Width;              // client width
            var ch = Bounds.Height;             // client height
            
            var iratio =  iw/ih;
            var cratio = cw/ch;
            var ratio = iratio/cratio;
            _minZoom = 1.0;

            _fitHZoom = ch / ih;
            _fitWZoom = cw/iw;
            
            if (ratio < 1)
            {
                _fitZoom = _fitHZoom;
                if (ih >= ch) _minZoom = _fitZoom;
            } 
            else if (ratio >= 1)
            {
                _fitZoom = _fitWZoom;
                if(iw >= cw)_minZoom = _fitZoom;
            } 
        }

        void AdjustPanOffset()
        {
            if (_image == null || Bounds.Width == 0 || Bounds.Height == 0)
            {
                _panOffset = new Point(0, 0);
                return;
            }

            var iw = (double)_image.PixelSize.Width;
            var ih = (double)_image.PixelSize.Height;
            var cw = Bounds.Width;
            var ch = Bounds.Height;



            var scaledWidth = iw * _zoom;
            var scaledHeight = ih * _zoom;

            _noScrolling = scaledHeight <= ch;

            double x, y;

            // Horizontal offset
            if (scaledWidth < cw)
                x = (cw - scaledWidth) / 2; // center
            else
                x = Math.Clamp(_panOffset.X, cw - scaledWidth, 0); // clamp within bounds

            // Vertical offset
            if (scaledHeight < ch)
                y = (ch - scaledHeight) / 2; // center
            else
                y = Math.Clamp(_panOffset.Y, ch - scaledHeight, 0); // clamp within bounds

            _panOffset = new Point(x, y);

            if (InScrollMode)
            {
                if (_topHit || _bottomHit)
                {
                    CancelTurnPageTimer(Edge.Bottom);
                    CancelTurnPageTimer(Edge.Top);
                    //Console.WriteLine("Cancel Turn Page Timer");       
                }

                if (_turnPageOnScrollDown || _turnPageOnScrollUp)
                {
                    _turnPageOnScrollDown = false;
                    _turnPageOnScrollUp = false;
                    //Console.WriteLine("Cancel Turn Page greenlight");
                }    
            }
        }
        //Pan & Zoom
        private void PanTo(Point newPanOffset)
        {
            _panOffset = newPanOffset;
            AdjustPanOffset();
            InvalidateVisual();
        }

        //to try sometimes : set anchor to the mouse pos in client coordinates to see how it feels
        //Default anchor : center of the viewport
        //Default panOffset : _panOffset
        private void ZoomTo(double zoomfactor, Point? panOffset = null, Point? anchor = null)
        {
            var oldZoom = _zoom;
            
            _zoom = Math.Clamp(zoomfactor, _minZoom, _maxZoom);

            var centerBefore = anchor ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
            
            var imageCenterBefore = (centerBefore - _panOffset) / oldZoom;
            _panOffset = centerBefore - imageCenterBefore * _zoom;


            AdjustPanOffset();
            InvalidateVisual();
        }

        private void Scroll(double deltaX, double deltaY)
        {
            if (_image == null) return;
            if ((_turnPageOnScrollDown || _noScrolling) && deltaY < 0)
            {
                _turnPageOnScrollDown = false;
                this.MainViewModel?.Next();
                return;
            }
            else if ((_turnPageOnScrollUp || _noScrolling) && deltaY > 0)
            {
                _turnPageOnScrollUp = false;
                this.MainViewModel?.Previous();
                return;
            }
            // Sensitivity factor — tune this
            const double scrollSpeed = 40.0;

            // Adjust pan offset
            var newPan = _panOffset + new Point(deltaX * scrollSpeed, deltaY * scrollSpeed);

            _panOffset = newPan;
            AdjustPanOffset();

            InvalidateVisual();

            // Notify scroll edge hits
            var ih = _image.PixelSize.Height * _zoom;
            var ch = Bounds.Height;

            bool hitTop = ih >= ch && _panOffset.Y >= 0;
            bool hitBottom = ih >= ch && _panOffset.Y <= ch - ih;
            /*
            bool hitLeft = iw >= cw && _panOffset.X >= 0;
            bool hitRight = iw >= cw && _panOffset.X <= cw - iw;
            */
            
            if (hitTop) OnScrollEdgeHit(Edge.Top);
            else if (_topHit) CancelTurnPageTimer(Edge.Top);
            else if (_turnPageOnScrollUp)
            {
                //was greenlit for turning page up, but scroll down reset everything
                _turnPageOnScrollUp = false;
                InvalidateVisual();
            }
            
            if (hitBottom) OnScrollEdgeHit(Edge.Bottom);
            else if (_bottomHit) CancelTurnPageTimer(Edge.Bottom);
            else if (_turnPageOnScrollDown) 
            {
                //was greenlit for turning page down, but scroll up reset everything
                _turnPageOnScrollDown = false;
                InvalidateVisual();
            }
/*
            if (hitLeft) OnScrollEdgeHit(Edge.Left);
            else if (hitRight) OnScrollEdgeHit(Edge.Right);
*/
        }
        
        // Restore from IVP parameters
        private void RestoreFromIVP(ImageViewingParams ivp)
        {

            if (_image == null || Bounds.Width == 0 || Bounds.Height == 0)
                return;

            var viewCenterX = Bounds.Width / 2.0;
            var viewCenterY = Bounds.Height / 2.0;

            _zoom = ivp.zoom;

            _panOffset = new Point(
                viewCenterX - ivp.centerX * _zoom,
                viewCenterY - ivp.centerY * _zoom
            );

                
            AdjustPanOffset();
            InvalidateVisual();
        }

        
        private void AnimateIVP(ImageViewingParams end, LibraryConfig.FitTypes? startFit = null)
        {
            if(_ivpAnim?.IsRunning == true)_ivpAnim.Stop();
            
            if (_page == null || _image == null) return;
            if (startFit != null)
            {
                switch (startFit)
                {
                    case LibraryConfig.FitTypes.Height:
                        FitHeight();
                        break;
                    case LibraryConfig.FitTypes.Width:
                        FitWidth();
                        break;
                    case LibraryConfig.FitTypes.Stock:
                        FitStock();
                        break;
                    default:
                        Fit();
                        break;
                }
            }
            var start = GetIVP();
            if (start == null) return;
            
            _ivpAnim = new IvpAnimation(start, end,
                (ivp) =>
                {
                    RestoreFromIVP(ivp);
                    if (_ivpAnim?.IsRunning == false) _ivpAnim = null;
                    //Console.WriteLine("animation tick");
                }, 
                AppStorage.Instance.UserSettings.IvpAnimSpeed);
        }

        private ImageViewingParams? GetIVP()
        {
            if (_image == null || Bounds.Width == 0 || Bounds.Height == 0)
                return null;

            double leftPx = -_panOffset.X;
            double topPx = -_panOffset.Y;

            double centerXpx = (leftPx + Bounds.Width / 2.0) / _zoom;
            double centerYpx = (topPx + Bounds.Height / 2.0) / _zoom;

            return new ImageViewingParams("", _zoom, centerXpx, centerYpx);
        }

        private ImageViewingParams ClampIvpToZoomLimits(ImageViewingParams ivp)
        {
            double clampedZoom = Math.Clamp(ivp.zoom, _minZoom, _maxZoom);

            if (Math.Abs(clampedZoom - ivp.zoom) < 0.0001)
                return ivp; // zoom is already within limits, no change

            return new ImageViewingParams(ivp.filename, clampedZoom, ivp.centerX, ivp.centerY);
        }




        public void OnStickPan(StickEventArgs e)
        {
            // Ignore input if stick is moving back toward neutral (delta opposite sign)
            if ((e.Y > 0 && e.DeltaY < 0) || (e.Y < 0 && e.DeltaY > 0)) return;
            var sensitivity = MapSensitivity(AppStorage.Instance.UserSettings.ControllerStickSensitivity);
            // Scale pan speed based on zoom level
            var scale = Math.Clamp(_zoom, 0.25, 4.0); // Avoid extreme panning at zoom extremes
            var delta = new Point(e.X, e.Y) * 15 * scale * sensitivity;
            PanTo(_panOffset - delta);
        }

        public void OnStickZoom(StickEventArgs e)
        {
            // Ignore input if stick is moving back toward neutral (delta opposite sign)
            if ((e.Y > 0 && e.DeltaY < 0) || (e.Y < 0 && e.DeltaY > 0)) return;
            var sensitivity = MapSensitivity(AppStorage.Instance.UserSettings.ControllerStickSensitivity);
            // Adjust zoom factor based on current zoom level to keep zoom rate stable
            var zoomAdjustment = _zoom * (e.Y * -0.016 * sensitivity); // Negative to zoom in with upward stick
            ZoomTo(_zoom + zoomAdjustment);
        }

        private double _lastPinchScale=1;
        public void OnPinched(object? sender, PinchEventArgs e)
        {
            //Do not fix floating point comparison here
            //e.Scale is reset to precisely 1 at Pinch Start
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (e.Scale == 1.0) _lastPinchScale = 1.0; 
            
            double multiplier = 1.0 + (e.Scale - _lastPinchScale);
            _lastPinchScale = e.Scale;
            
            ZoomTo(_zoom * multiplier);
            e.Handled = true;
        }
        public void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_image == null) return;
            
            var pointerProperties = e.GetCurrentPoint(this).Properties;
            var current = e.GetPosition(this);
            var delta = current - _lastPointerPosition;
            _lastPointerPosition = current;
            
            InputManager.MouseButton? button = null;
            
            if (pointerProperties.IsLeftButtonPressed)
                button = InputManager.MouseButton.LeftMouseButton;
            else if (pointerProperties.IsMiddleButtonPressed)
                button = InputManager.MouseButton.MiddleMouseButton;
            else if (pointerProperties.IsRightButtonPressed)
                button = InputManager.MouseButton.RightMouseButton;

            if (button == InputManager.Instance.DragPanButton)
            {
                PanTo(_panOffset + delta);
            }
            else if (button == InputManager.Instance.DragZoomButton)
            {
                var sensitivity = MapSensitivity(AppStorage.Instance.UserSettings.MouseSensitivity);
                ZoomTo(_zoom * Math.Pow(1.01, sensitivity * -delta.Y));
            }
            else if (button == InputManager.Instance.DrawZoomRectButton && UseIvp)
            {
                if (_ivpRect == null)
                {
                    _ivpRect = new IvpRect() { Start = current, End = current };
                }
                else
                {
                    _ivpRect.End = current;
                    InvalidateVisual();
                }    
            }
        }
        public void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _lastPointerPosition = e.GetPosition(this);
        }
        
        DateTime _lastTap = DateTime.MinValue;
        private const int DoubleTapThresholdMs = 300;
        public void OnPointerReleased(object? sender, PointerEventArgs e)
        {
            var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
            InputManager.MouseButton? button = kind switch
            {
                PointerUpdateKind.LeftButtonReleased => InputManager.MouseButton.LeftMouseButton,
                PointerUpdateKind.RightButtonReleased => InputManager.MouseButton.RightMouseButton,
                PointerUpdateKind.MiddleButtonReleased =>InputManager.MouseButton.MiddleMouseButton,
                _ => null
            };
            if(button == null )return;
            
            var now = DateTime.Now;
            if ( _page != null &&  UseIvp && _ivpRect != null && button == InputManager.Instance.DrawZoomRectButton)
            {
                if (DateTime.Now - _ivpRect.StartTime < TimeSpan.FromMilliseconds(500))
                {
                    _ivpRect = null;
                    InvalidateVisual();
                    return;
                }
                
                _ivpRect.End = e.GetPosition(this);
                    
                var ivp = _ivpRect?.ToIvp(_page.Name, _panOffset, _zoom, Bounds.Size);
                //var ivp = _ivpRect?.ToIvpFit(_page.Name, Bounds.Size, _panOffset);
                if (ivp != null)
                {
                    ivp = ClampIvpToZoomLimits(ivp);
                    if (Config?.AnimateIVPs==true) AnimateIVP(ivp);
                    else RestoreFromIVP(ivp);
                }
                _ivpRect = null;
            } 
            else if(kind == PointerUpdateKind.LeftButtonReleased || e.Pointer.Type == PointerType.Touch)
            {
                //Double tap detection that can handle touch
                var elapsed = now - _lastTap;

                if (elapsed.TotalMilliseconds <= DoubleTapThresholdMs)
                {
                    _lastTap = DateTime.MinValue; // Reset after double-tap
                    MainViewModel?.ToggleFullscreenCommand.Execute(null);
                }
                else
                {
                    _lastTap = now;
                }
            }
            
        }
        public void OnMouseWheel(object? sender, PointerWheelEventArgs e)
        {
            if (Config?.ScrollAction == LibraryConfig.ScrollActions.TurnPage || !_isFullscreen )
            {
                if (Math.Abs(e.Delta.Y - (-1.0)) < 0.01f) _ = MainViewModel?.Next();
                
                else if (Math.Abs(e.Delta.Y - 1.0) < 0.01f) _ = MainViewModel?.Previous(); 
            }
            else
            {
                if (_image == null || !InScrollMode)
                    return;
            
                // Scrolling deltas (usually Y for vertical scrolling, but X can be used for shift+wheel or trackpads)
                var delta = e.Delta;
            
                Scroll(delta.X, delta.Y);    
            }
        }


        
        private void OnTurnPageTick(object? sender, EventArgs e)
        {
            if(_topHit) _turnPageOnScrollUp = true;
            else if(_bottomHit) _turnPageOnScrollDown = true;
            
            _topHit = _bottomHit = false;
            turnPageTimer.Stop();
            InvalidateVisual();
        }
        
        private void OnScrollEdgeHit(Edge edge)
        {
            if (edge == Edge.Top) _topHit = true;
            else if (edge == Edge.Bottom) _bottomHit = true;
            turnPageTimer.Start();
        }

        void CancelTurnPageTimer(Edge edge)
        {
            if (edge == Edge.Top) _topHit = false;
            else if (edge == Edge.Bottom) _bottomHit = false;
            turnPageTimer.Stop();
        }

        public void FitHeight()
        {
            if (_image == null) return;
            _zoom  = _fitHZoom;
            AdjustPanOffset();
            InvalidateVisual();
        }
        public void FitWidth()
        {
            if (_image == null) return;
            _zoom  = _fitWZoom;
            AdjustPanOffset();
            InvalidateVisual();
        }

        public void Fit()
        {
            if (_image == null) return;
            _zoom  = _fitZoom;
            AdjustPanOffset();
            InvalidateVisual();
        }

        public void FitStock()
        {
            if (_image == null) return;
            _zoom = 1;
            AdjustPanOffset();
            InvalidateVisual();
        }

        public void OnDownArrowPressed()
        {
            Scroll(0, -1.0);
        }

        public void OnUpArrowPressed()
        {
            Scroll(0, 1.0);
        }

        public void OnLeftArrowPressed()
        {
            Scroll(-1.0, 0);
        }

        public void OnRightArrowPressed()
        {
            Scroll(1.0, 0);
        }

        public void Zoom(int delta)
        {
            const double zoomStepFactor = 0.1; // 10% zoom step
            double zoomDelta = _zoom * zoomStepFactor * Math.Sign(delta);
            ZoomTo(_zoom + zoomDelta);
        }
        
        double MapSensitivity(double value)
        {
            value = Math.Clamp(value, 0.0, 1.0);

            if (value <= 0.5)
            {
                // Map [0, 0.5] to [0.1, 1]
                return 0.1 + (value / 0.5) * (1.0 - 0.1);
            }
            else
            {
                // Map (0.5, 1] to [1, 10]
                return 1.0 + ((value - 0.5) / 0.5) * (10.0 - 1.0);
            }
        }

        private class IvpRect
        {
            public Point Start { get; init; }
            public Point End { get; set; }

            double top => Math.Min(Start.Y, End.Y);
            double left => Math.Min(Start.X, End.X);
            double right => Math.Max(Start.X, End.X);
            double bottom => Math.Max(Start.Y, End.Y);
            double width => right - left;
            double height => bottom - top;

            public bool IsValid => Math.Abs(Start.Y - End.Y) > 2.0 
                                   && Math.Abs(Start.X - End.X) > 2.0;

            public DateTime StartTime = DateTime.Now;

            public Rect ToRect()
            {
                return new Rect(left, top, width, height);
            }

            public ImageViewingParams ToIvp(string filename, Point panOffset, double zoom, Size clientSize)
            {
                double leftPx = left-panOffset.X;
                double topPx = top-panOffset.Y;
                double centerXpx = (leftPx + width / 2.0) / zoom;
                double centerYpx = (topPx + height / 2.0) / zoom;
                
                var zoomX = clientSize.Width / width;
                var zoomY = clientSize.Height / height;
                var newZoom = Math.Min(zoomX, zoomY) * zoom;
                
                return new ImageViewingParams(filename, newZoom, centerXpx, centerYpx);          
            }
        }

        private class IvpAnimation
        {
            private readonly ImageViewingParams _startIvp;
            private readonly ImageViewingParams _endIvp;
            public ImageViewingParams EndIvp => _endIvp;
            private readonly double _duration;
            private DispatcherTimer? _timer;
            private readonly Action<ImageViewingParams> _onTick;
            private readonly DateTime _startTime;
            private bool _running = true;
            public bool IsRunning => _running;
            
            //duration in milliseconds
            public IvpAnimation(ImageViewingParams startIvp, ImageViewingParams endIvp, Action<ImageViewingParams> onTick, double duration)
            {
                _startIvp = startIvp;
                _endIvp = endIvp;
                _onTick = onTick;
                _duration = duration;
                _timer = new DispatcherTimer(DispatcherPriority.Render);
                _timer.Interval = TimeSpan.FromMilliseconds(16); // 60fps
                _startTime = DateTime.Now;
                _timer.Tick += OnTick;
                _timer.Start();
            }

            private void OnTick(object? sender, EventArgs e)
            {
                var elapsed = DateTime.Now - _startTime;
                var tLinear = Math.Clamp(elapsed.TotalMilliseconds / _duration, 0, 1);
                var t = EaseInOutQuad(tLinear);
                if (t >= 1.0)
                {
                    Stop();
                    _onTick(_endIvp); 
                }
                double zoom = Lerp(_startIvp.zoom, _endIvp.zoom, t);
                double centerX = Lerp(_startIvp.centerX, _endIvp.centerX, t);
                double centerY = Lerp(_startIvp.centerY, _endIvp.centerY, t);
                _onTick(new ImageViewingParams(_startIvp.filename, zoom, centerX, centerY));
            }

            public void Stop()
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTick;
                    _timer = null;
                    _running = false;
                }
            }
            
            static double Lerp(double from, double to, double t)
            {
                return from + (to - from) * t;
            }
            
            private static double EaseInOutQuad(double t)
            {
                return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
            }
        }



    }
}