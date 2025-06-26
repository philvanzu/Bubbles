using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using System;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Custom;
using Bubbles4.Models;
using Bubbles4.ViewModels;

namespace Bubbles4.Controls {
    public class FastImageViewer : Control
    {
        public enum Edge { Top, Bottom, Left, Right }
        
        private Point _panOffset = new(0, 0);
        private double _zoom = 1.0;
        private double _minZoom = 1.0;
        private double _fitZoom = 1.0;
        private double _fitWZoom = 1.0;
        private double _fitHZoom = 1.0;
        private readonly double _maxZoom = 10.0;
        private Bitmap? _image;
        private Point _lastPointerPosition;
        private Size _lastViewportSize = new Size(0, 0);
        private PageViewModel? _page = null;  
        private BookViewModel? _previousBook = null;
        
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

        public static readonly StyledProperty<bool> IsFullscreenProperty =
            AvaloniaProperty.Register<FastImageViewer, bool>(nameof(IsFullscreen));
        public bool IsFullscreen
        {
            get => GetValue(IsFullscreenProperty);
            set => SetValue(IsFullscreenProperty!, value);
        }
        bool UseIvp => IsFullscreen && Config != null && Config.UseIVPs;
        bool InScrollMode => IsFullscreen && Config?.ScrollAction == LibraryConfig.ScrollActions.Scroll;
        bool BookChanged => _page?.Book != _previousBook;
        private bool KeepZoom => IsFullscreen && Config?.LookAndFeel == LibraryConfig.LookAndFeels.Reader && !BookChanged; 

        public FastImageViewer()
        {
            Focusable = true;
            this.Focus();
            this.PointerPressed += (s, e) => _lastPointerPosition = e.GetPosition(this);
            this.PointerMoved += OnPointerMoved;
            this.LayoutUpdated += OnLayoutUpdated;
            this.KeyUp += OnKeyUp;
            
            turnPageTimer = new DispatcherTimer();
            turnPageTimer.Tick += OnTurnPageTick;
            turnPageTimer.Interval = TimeSpan.FromMilliseconds(1000);
            
            
        }



        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            switch (change.Property.Name)
            {
                case nameof(Data):
                    if (BookChanged) _previousBook = _page?.Book;
                    var data = change.NewValue as ViewerData;
                    
                    if (_page != null && UseIvp )
                        _page.Ivp = SaveToIVP(_page.Name);

                    bool isnext = true;
                    if (!BookChanged) isnext = data?.Page.Index - _page?.Index > 0;
                    //Console.WriteLine($"isnext :{isnext}");
                    
                    _image = data?.Image;
                    _page = data?.Page;
                    
                    if (_image != null)
                    {
                        AdjustZoomLimits();
                        if (Data != null)
                        {
                            if (IsFullscreen)
                            {
                                var ivp = _page?.Ivp;
                                if (ivp != null && UseIvp)
                                    RestoreFromIVP(ivp);
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
                    InvalidateVisual();
                    break;
                case nameof(Config):
                    if (Config != null && UseIvp)
                    {
                        // load ivp?
                        // save ivp?
                    }
                    break;
                   
            }

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
            var pen = new Pen(Brushes.Black, 1);

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

            if (ratio < 1)
            {
                _fitZoom = _fitHZoom = ch / ih;
                if (ih >= ch) _minZoom = _fitZoom;
            } 
            else if (ratio >= 1)
            {
                _fitZoom = _fitWZoom = cw/iw;
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
        public void PanTo(Point newPanOffset)
        {
            _panOffset = newPanOffset;
            AdjustPanOffset();
            InvalidateVisual();
        }

        //to try sometimes : set anchor to the mouse pos in client coordinates to see how it feels
        //Default anchor : center of the viewport
        //Default panOffset : _panOffset
        public void ZoomTo(double zoomfactor, Point? panOffset = null, Point? anchor = null)
        {
            var oldZoom = _zoom;
            
            _zoom = Math.Clamp(zoomfactor, _minZoom, _maxZoom);

            var centerBefore = anchor ?? new Point(Bounds.Width / 2, Bounds.Height / 2);
            var offset = panOffset ?? _panOffset;
            
            var imageCenterBefore = (centerBefore - _panOffset) / oldZoom;
            _panOffset = centerBefore - imageCenterBefore * _zoom;


            AdjustPanOffset();
            InvalidateVisual();
        }


        // Restore from IVP parameters
         public void RestoreFromIVP(ImageViewingParams ivp)
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
        public ImageViewingParams? SaveToIVP(string filename)
        {
            if (_image == null || Bounds.Width == 0 || Bounds.Height == 0)
                return null;

            double leftPx = -_panOffset.X;
            double topPx = -_panOffset.Y;

            double centerXpx = (leftPx + Bounds.Width / 2.0) / _zoom;
            double centerYpx = (topPx + Bounds.Height / 2.0) / _zoom;

            return new ImageViewingParams(filename, _zoom, centerXpx, centerYpx);
        }

        

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (_image == null)
                return;

            var newSize = Bounds.Size;

            // Only apply proportional zoom change if both old and new sizes are valid
            if (_lastViewportSize.Width > 0 && _lastViewportSize.Height > 0 &&
                newSize.Width > 0 && newSize.Height > 0)
            {
                // Use height as the reference axis
                double heightRatio = newSize.Height / _lastViewportSize.Height;
                var oldZoom = _zoom;
                _zoom *= heightRatio;
                _zoom = Math.Clamp(_zoom, _minZoom, _maxZoom);

                // Maintain the position of the image center relative to viewport center
                var centerBefore = new Point(_lastViewportSize.Width / 2, _lastViewportSize.Height / 2);
                var imageCenterBefore = (centerBefore - _panOffset) / oldZoom;
                var centerAfter = new Point(newSize.Width / 2, newSize.Height / 2);
                _panOffset = centerAfter - imageCenterBefore * _zoom;
            }

            _lastViewportSize = newSize;
            AdjustZoomLimits();
            AdjustPanOffset();
            InvalidateVisual();
        }
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_image == null) return;

            var pointerProperties = e.GetCurrentPoint(this).Properties;
            var current = e.GetPosition(this);
            var delta = current - _lastPointerPosition;
            _lastPointerPosition = current;

            if (pointerProperties.IsLeftButtonPressed)
            {
                PanTo(_panOffset + delta);
            }
            else if (pointerProperties.IsMiddleButtonPressed)
            {
                ZoomTo(_zoom * Math.Pow(1.01, -delta.Y));
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.H)
            {
                e.Handled = true;
                FitHeight();
            }
            else if (e.Key == Key.W)
            {
                e.Handled = true;
                FitWidth();
            }
            else if (e.Key == Key.F)
            {
                e.Handled = true;
                Fit();
            }
        }

        public void OnScroll(object? sender, PointerWheelEventArgs e)
        {
            
            if (_image == null || !InScrollMode)
                return;
            
            

            // Scrolling deltas (usually Y for vertical scrolling, but X can be used for shift+wheel or trackpads)
            var delta = e.Delta;
            
            if ((_turnPageOnScrollDown || _noScrolling) && e.Delta.Y < 0)
            {
                _turnPageOnScrollDown = false;
                this.MainViewModel?.Next();
                return;
            }
            else if ((_turnPageOnScrollUp || _noScrolling) && e.Delta.Y > 0)
            {
                _turnPageOnScrollUp = false;
                this.MainViewModel?.Previous();
                return;
            }
            // Sensitivity factor — tune this
            const double scrollSpeed = 40.0;

            // Adjust pan offset
            var newPan = _panOffset + new Point(delta.X * scrollSpeed, delta.Y * scrollSpeed);

            var oldPan = _panOffset;
            _panOffset = newPan;
            AdjustPanOffset();

            InvalidateVisual();

            // Notify scroll edge hits
            var iw = _image.PixelSize.Width * _zoom;
            var ih = _image.PixelSize.Height * _zoom;
            var cw = Bounds.Width;
            var ch = Bounds.Height;

            bool hitTop = ih >= ch && _panOffset.Y >= 0;
            bool hitBottom = ih >= ch && _panOffset.Y <= ch - ih;
            bool hitLeft = iw >= cw && _panOffset.X >= 0;
            bool hitRight = iw >= cw && _panOffset.X <= cw - iw;

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
            var ih = (double)_image.PixelSize.Height;  // image height
            var ch = Bounds.Height;
            _zoom  = ch/ih;
            AdjustPanOffset();
            InvalidateVisual();
        }
        public void FitWidth()
        {
            if (_image == null) return;
            var iw = (double)_image.PixelSize.Width;   // image width
            var cw = Bounds.Width;              // client width
            _zoom  = cw/iw;
            AdjustPanOffset();
            InvalidateVisual();
        }

        public void Fit()
        {
            var iw = (double)_image.PixelSize.Width;   // image width
            var ih = (double)_image.PixelSize.Height;  // image height
            var cw = Bounds.Width;              // client width
            var ch = Bounds.Height;             // client height
            
            var ratio = (iw/ih) * (ch/cw);
                    
            if (ratio < 1) FitHeight();
            else FitWidth();
        }

        public void FitStock()
        {
            _zoom = 1;
            AdjustPanOffset();
            InvalidateVisual();
        }
        

    }
}