using System;
using System.Collections.Generic;
using System.ComponentModel;
using Bubbles3.Models;

using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

using Bitmap = SharpDX.Direct2D1.Bitmap;
using System.Windows.Forms;
using System.Threading;
using Bubbles3.Utils;
using Bubbles3.ViewModels;
using System.Timers;

namespace Bubbles3.Controls
{
    public partial class BblImageSurface : D2D1Control
    {
        const float maxZoom = 5f;

        private SolidColorBrush _blackBrush, _redBrush, _greenBrush, _whiteBrush, _greyBrush;
        private TextFormat _textFormat;
        private CustomTextRenderer _outlineTextRenderer;

        private const double rad2deg = 180 / Math.PI;
        private const double deg2rad = 1 / rad2deg;

        private Matrix3x2 _rotation = Matrix3x2.Identity;
        private Matrix3x2 _scale = Matrix3x2.Identity;
        private Matrix3x2 _translation = Matrix3x2.Identity;

        private DateTime _turnPageTimer;
        private System.Timers.Timer _tpTimer;
        private SynchronizationContext _syncontext;
        private Vector2 _scrollIndicator;

        private System.Drawing.Point _mousePos, _mouseHiddenPos;
        bool _lmb = false;
        bool _mmb = false;
        //bool _rmb = false;

        

        private System.Timers.Timer _hideCursorTimer;
        private bool _cursorVisible;

        Bubbles3.ViewModels.PageViewModel _page;
        Bitmap _img;
        BblAnimation _anim;

        TabOptions _p;
        public TabOptions P
        {
            get { return _p; }
            set
            {
                _p = value;

                if (_p.rememberView == true && _page?.Model?.Ivp != null && !_page.Model.Ivp.isReset)
                {
                    _ivp = _page.Model.Ivp;
                    RestoreView();
                }
                else
                {
                    if (!_p.rememberView) _ivp.Reset();

                    switch (_p.zoomMode)
                    {
                        case BblZoomMode.Default:
                            ResetView();
                            break;
                        case BblZoomMode.Fit:
                            Fit();
                            break;
                        case BblZoomMode.FitH:
                            FitH();
                            break;
                        case BblZoomMode.FitW:
                            FitW();
                            break;
                    }
                }
            }
        }

        float zoomf => _scale.M11;
        float _lastzf;


        SharpDX.WIC.ImagingFactory _wicFactory;
        SharpDX.WIC.ImagingFactory ImagingFactory
        {
            get
            {
                if (this._wicFactory == null)
                {
                    this._wicFactory = new SharpDX.WIC.ImagingFactory();
                }
                return this._wicFactory;
            }
        }

        SharpDX.DirectWrite.Factory _dwFactory;
        SharpDX.DirectWrite.Factory DirectWriteFactory
        {
            get
            {
                if (this._dwFactory == null)
                {
                    this._dwFactory = new SharpDX.DirectWrite.Factory(SharpDX.DirectWrite.FactoryType.Shared);
                }
                return this._dwFactory;
            }
        }

        /// <summary>
        ///returns the center point of the client window transformed
        ///by the inverse of the transformation matrix
        /// </summary>
        Vector2 clientCenterProj
        {
            get
            {
                if (_img != null)
                {
                    Matrix3x2 t = _translation; 
                    t.Invert();
                    return Matrix3x2.TransformPoint(t, ClientCenter);
                }
                else return Vector2.Zero;
            }
        }
        Vector2 ClientCenter { get; set; }

        Vector2 ImgCenter { get; set; }
        Size2 ImgSize => (_img != null) ? _img.PixelSize : Size2.Zero;

        bool _bottomHit, _topHit, _navBack;

        bool _freerot;
        Vector3 _freeRotInitClick;
        float _freeRotInitAngle;
        Vector2 _freeRotCenter;

        ImageViewingParams _ivp;
        Stack<ImageViewingParams> _undostack = new Stack<ImageViewingParams>();

        bool _drawingZoomRect;
        bool _doDrawZoom;
        System.Drawing.Rectangle _zoomRect;

        System.Timers.Timer _scrollTimer;
        Vector2 _scrollStart;
        Vector2 _scrollEnd;
        DateTime _scrollStartTime;
        static readonly TimeSpan _scrollSpan = new TimeSpan(0, 0, 0, 0, 150);
        bool _scrollingUp;


        #region CONSTRUCTOR
        public BblImageSurface()
        {
            InitializeComponent();

            _translation = _scale = _rotation = Matrix3x2.Identity;

            _turnPageTimer = DateTime.Now.AddYears(100);
            _tpTimer = new System.Timers.Timer();
            _tpTimer.Elapsed += TurnPageTimer_Elapsed;
            _tpTimer.AutoReset = false;
            _tpTimer.Interval = 500;

            _scrollTimer = new System.Timers.Timer();
            _scrollTimer.Elapsed += ScrollTick;
            _scrollTimer.AutoReset = true;
            _scrollTimer.Interval = 1;

            _cursorVisible = true;
            _syncontext = SynchronizationContext.Current;
        }
        #endregion CONSTRUCTOR

        #region public methods



        
        public void LoadPage(PageViewModel page)
        {
            if (page == _page) return;
            if (_page?.Model != null) UnloadPage();

            _page = page;
            
            
            
            _page.Model.LoadImageAsync(0, OnPageBitmapLoaded, null);
             
        }

        public void UnloadPage()
        {
            _lastzf = _scale.M11;
            _undostack.Clear();
            if (_anim.isRunning && _page?.Model != null)
            {
                _anim.EndNow();
                SaveIVP();
            }

            if (_img != null) UnloadImage();
            _ivp.Reset();
        }

        public void OnPageBitmapLoaded(object sender, EventArgs args)
        {
            if (sender != _page.Model) return;

            if(_p.rememberView  && _page.Model.Ivp != null) _ivp = _page.Model.Ivp;
            else
            {

            }
            
            if (_page.Model.Image != null && DC != null)
            {

                _img = _page.Model.Image.GetD2DBitmap(DC);
                

                ImgCenter = new Vector2(_img.PixelSize.Width / 2f, _img.PixelSize.Height / 2f);

                float cw = ClientSize.Width;
                float ch = ClientSize.Height;
                float iw = ImgSize.Width;
                float ih = ImgSize.Height;

                if (iw == 0 || ih == 0)
                    return;

                float mz, mhz, mvz;                             // min horizontal and vertical zoomfactors
                mhz = cw / iw;
                mvz = ch / ih;
                mz = Math.Min(mhz, mvz);                        // min zoom is the smaller of the two


                float z = zoomf;
                if (Single.IsInfinity(z) || Single.IsNegativeInfinity(z) || Single.IsNaN(z) || z == 0)
                    z = (!Single.IsInfinity(_lastzf) || Single.IsNegativeInfinity(_lastzf) || Single.IsNaN(_lastzf)) ? _lastzf : 0;

                _ivp.Reset();
                _ivp.filename = _page.Model.Filename;
                _scale = _translation = _rotation = Matrix3x2.Identity;    // reset rotation and translation

                //default zoom mode if keep zoom option inactive
                if (!P.keepZoom || z == 1f || z == 0)
                {
                    if (P.zoomMode == BblZoomMode.Default) z = 1f;
                    else if (P.zoomMode == BblZoomMode.Fit) z = mz;
                    else if (P.zoomMode == BblZoomMode.FitW) z = mhz;
                    if (P.zoomMode == BblZoomMode.FitH) z = mvz;
                }
                //recenter the scaling on img origin
                _scale = Matrix3x2.Scaling(z, z, Vector2.Zero);


                if (P.readBackwards && _navBack)
                {
                    // bottom right first when reading backwards (setting must be enabled)
                    _navBack = false;
                    _translation = Matrix3x2.Translation((-ImgSize.Width * z), (-ImgSize.Height * z));
                }

                //Restore Previous IVP if Remember option is active.
                if (P.rememberView && !_page.Model.Ivp.br.Equals(Vector2.Zero))
                {
                    if (P.animIVP && _ivp != _page.Model.Ivp)
                    {
                        //compute the first frame's ivp
                        UpdateRenderParams(true);
                        MakeIVP();
                        ImageViewingParams tmp = _ivp;

                        //compute the last frame's ivp with  constraints,
                        // to avoid hiccups when
                        // the constraints kick in at last frame
                        _ivp = _page.Model.Ivp;
                        RestoreView();
                        UpdateRenderParams(true);
                        MakeIVP();


                        //launch unconstrained animation
                        _anim.Init(tmp, _ivp, OnAnimationTick, 500, 500);
                    }
                    else
                    {
                        _ivp = _page.Model.Ivp;
                        RestoreView();
                    }
                }

                //overwrite the page ivp with the current one.
                //_page.Model.Ivp = _ivp;

                //Set UI right
                //ImageOrientationChanged(_ivp.rotation);
                
                UpdateRenderParams();

                Bubbles3.ViewModels.ShellViewModel.Instance.ActiveTab.PageSize = ImgSize.Width.ToString() + " X " + ImgSize.Height.ToString();
                
            }
            
        }

        public void UnloadImage()
        {
            if (_img != null && !_img.IsDisposed) _img.Dispose();
            _img = null;
            ImgCenter = Vector2.Zero;
        }
        #endregion

        #region Request events
        public event EventHandler fullscreenToggleRequested;
        public event EventHandler nextPageRequested;
        public event EventHandler prevPageRequested;
        /// <summary>
        /// called when the user tries to scroll past the image's bottom border
        /// </summary>
        protected void RequestNext()
        {
            if (_turnPageTimer <= DateTime.Now.AddMilliseconds(-500))
            {
                _turnPageTimer = DateTime.Now.AddYears(100);
                if (nextPageRequested != null)
                {
                    nextPageRequested(this, EventArgs.Empty);
                }
            }
            else
            {
                _turnPageTimer = DateTime.Now;
                _tpTimer.Stop();
                _tpTimer.Start();
            }
        }
        /// <summary>
        /// called when the user tries to scroll past the image's top border
        /// </summary>
        protected void RequestPrevious()
        {
            if (_turnPageTimer <= DateTime.Now.AddMilliseconds(-500))
            {
                _turnPageTimer = DateTime.Now.AddYears(100);
                _navBack = true;
                if (prevPageRequested != null)
                {
                    prevPageRequested(this, EventArgs.Empty);
                }
            }
            else
            {
                _turnPageTimer = DateTime.Now;
                _tpTimer.Stop();
                _tpTimer.Start();
            }
        }

        private void TurnPageTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _syncontext.Post(o => Invalidate(), null);
        }

        # endregion

        #region User Input
        public void OnIvpPredicted(float center, float top, float bottom)
        {

        }
        public void OnLeft(bool repeat=false)
        {
            float shift = (repeat)? 2 : 20;
            Translate(shift , 0, false, !repeat);
        }
        public void OnRight(bool repeat = false)
        {
            float shift = (repeat) ? -2 : -20;
            Translate(shift , 0, false, !repeat);
        }
        public void OnUp(bool repeat = false)
        {
            float shift = (repeat) ? 2 : 20;
            Translate(0, shift, false, !repeat);
        }
        public void OnDown(bool repeat = false)
        {
            float shift = (repeat) ? -2 : -20;
            Translate(0, shift, false, !repeat);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            switch (e.Button)
            {
                case MouseButtons.Left:
                    _lmb = true;
                    if (((Control.ModifierKeys & Keys.Control) == Keys.Control))
                    {
                        if (!P.zoomRectOnRightClick)
                        {
                            _drawingZoomRect = true;
                            DrawZoomRect(e.X, e.Y);
                        }
                    }
                    else if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    {
                        _freerot = true;
                        FreeRotation((float)e.X, (float)e.Y, true);
                    }
                    break;
                case MouseButtons.Middle:
                    _mmb = true;
                    break;
                case MouseButtons.Right:
                    //if (!_cursorVisible) ResetCursorTimer();

//                    _rmb = true;
                    if (P.zoomRectOnRightClick)
                    {
                        _drawingZoomRect = true;
                        DrawZoomRect(e.X, e.Y);
                    }

                    break;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            switch (e.Button)
            {
                case MouseButtons.Left:
                    if (_freerot)
                    {
                        UpdateRenderParams();
                        _freerot = false;
                    }
                    _lmb = false;
                    if (_drawingZoomRect)
                    {
                        _drawingZoomRect = false;
                        ZoomRect();
                    }
                    break;
                case MouseButtons.Middle:
                    _mmb = false;
                    break;
                case MouseButtons.Right:
//                    _rmb = false;
                    break;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            //this.Focus();
            base.OnMouseEnter(e);
            //ResetCursorTimer();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hideCursorTimer.Stop();

            if (!_cursorVisible)
            {
                _cursorVisible = true;
                Cursor.Show();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_cursorVisible)
            {
                int deltaHx = Math.Abs(e.X - _mouseHiddenPos.X);
                int deltaHy = Math.Abs(e.Y - _mouseHiddenPos.Y);
                int threshold = 25;
                if (deltaHx > threshold || deltaHy > threshold) ResetCursorTimer();
            }
            else ResetCursorTimer();

            int deltaX = e.X - _mousePos.X;
            int deltaY = e.Y - _mousePos.Y;
            _mousePos = e.Location;

            if (_mmb)
            {
                Zoom(-(float)deltaY, true);
            }
            else if (_drawingZoomRect)
            {
                DrawZoomRect(e.Location.X, e.Location.Y);
            }
            else if (_lmb)
            {
                if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                {
                    Zoom(-(float)(2 * deltaY), true);
                }
                else if (_freerot)
                {
                    FreeRotation((float)e.X, (float)e.Y);

                }
                else Translate(deltaX, deltaY, false);
            }
        }


        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if( e.Button == MouseButtons.Left && fullscreenToggleRequested != null ) fullscreenToggleRequested(this, EventArgs.Empty);

        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {

            if (P.mwScroll) Translate(0, e.Delta, true, true);
            else if (e.Delta < 0 && nextPageRequested != null) nextPageRequested(this, EventArgs.Empty);
            else if (e.Delta >= 0 && prevPageRequested != null) prevPageRequested(this, EventArgs.Empty);

            base.OnMouseWheel(e);

        }

        int _resizePreviewHPos;
        public void OnResizePreviewCompleted(System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _resizePreviewHPos = 0;
            Invalidate();
        }
        public void OnResizePreview(System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            if(e.HorizontalChange > 0)
            { 
                _resizePreviewHPos = (int) e.HorizontalChange;
                Invalidate();
            }
        }
        #endregion

        #region HideCursor
        protected void ResetCursorTimer()
        {
            if (_hideCursorTimer == null)
            {
                _hideCursorTimer = new System.Timers.Timer(1000);
                _hideCursorTimer.Elapsed += OnCursorTimerElapsed;
                _hideCursorTimer.AutoReset = false;
                _hideCursorTimer.Enabled = true;
            }
            _hideCursorTimer.Stop();
            _hideCursorTimer.Start();

            if (!_cursorVisible)
            {
                Cursor.Show();
                _cursorVisible = true;
            }
        }
        void OnCursorTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                //Timer Elapsed runs in a separate thread.
                Invoke(new MethodInvoker(() =>
                {
                    _hideCursorTimer.Enabled = false;
                    if (_cursorVisible)
                    {
                        _cursorVisible = false;
                        Cursor.Hide();
                        _mouseHiddenPos = _mousePos;
                    }
                }));
            }
            catch { }

        }
        #endregion HideCursor

        #region Create & cleanup Resources        

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _anim = new BblAnimation();
        }
        protected override void OnDispose()
        {
            if (_anim != null) _anim.Dispose();
            if (_wicFactory != null && !_wicFactory.IsDisposed) _wicFactory.Dispose();
            _wicFactory = null;

            if (_dwFactory != null && !_dwFactory.IsDisposed) _dwFactory.Dispose();
        }
        protected override void OnCreateDeviceResources()
        {
            if (DC == null) return;
            _blackBrush = new SolidColorBrush(DC, Color.Black);
            _redBrush = new SolidColorBrush(DC, Color.Red);
            _greenBrush = new SolidColorBrush(DC, Color.Green);
            _whiteBrush = new SolidColorBrush(DC, Color.White);
            _greyBrush = new SolidColorBrush(DC, Color.DimGray);
            _greyBrush.Opacity = 0.5f;
            if (P.showPaging && _page?.Model != null)
            {
                _outlineTextRenderer = new CustomTextRenderer(Direct2DFactory, DC, _whiteBrush, _blackBrush);
            }
        }
        protected override void OnCleanUpDeviceResources()
        {
            if (_outlineTextRenderer != null && !_outlineTextRenderer.IsDisposed)
            {
                _outlineTextRenderer.Dispose();
                _outlineTextRenderer = null;
            }
            if (_blackBrush != null && !_blackBrush.IsDisposed)
            {
                _blackBrush.Dispose();
                _blackBrush = null;
            }
            if (_greenBrush != null && !_greenBrush.IsDisposed)
            {
                _greenBrush.Dispose();
                _greenBrush = null;
            }
            if (_redBrush != null && !_redBrush.IsDisposed)
            {
                _redBrush.Dispose();
                _redBrush = null;
            }
            if (_whiteBrush != null && !_whiteBrush.IsDisposed)
            {
                _whiteBrush.Dispose();
                _whiteBrush = null;
            }
            if(_greyBrush != null && !_greyBrush.IsDisposed)
            {
                _greyBrush.Dispose();
                _greyBrush = null;
            }
        }

        protected override void OnCreateDeviceIndependentResources()
        {
            _textFormat = new TextFormat(DirectWriteFactory, "courier", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 12);
            _textFormat.TextAlignment = TextAlignment.Leading;
            _textFormat.ParagraphAlignment = ParagraphAlignment.Near;
            if (_page?.Model != null) _page.Model.LoadImageAsync(0, OnPageBitmapLoaded, null);
        }
        protected override void OnCleanUpDeviceIndependentResources()
        {
            if (_textFormat != null && !_textFormat.IsDisposed) _textFormat.Dispose();
            _textFormat = null;
            if (_img != null && !_img.IsDisposed) _img.Dispose();
            _img = null;
        }

        #endregion

        #region resize
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClientCenter = (new Vector2(ClientSize.Width / 2f, ClientSize.Height / 2f));
            Invalidate();
        }

        /// <summary>
        /// Adapt the matrices so that the image is resized harmoniously
        /// when the client is resized or when restoring last viewed params option.
        /// </summary>
        /// <param name="WidthSignificant"></param>
        protected void RestoreView()
        {
            IvpToTransform(_ivp, false, false, out _rotation, out _scale, out _translation);
        }

        /// <summary>
        /// calculate the transformations needed so that the viewport coincides with the
        /// provided ivp.
        /// </summary>
        /// <param name="ivp">the view to restore</param>
        /// <param name="widthSignificant">if true the width of the ivp will come into account for the restoration of the view</param>
        /// <param name="centered">if true the origin of the restoration will be the center of the ivp, not the tl corner</param>
        protected void IvpToTransform(ImageViewingParams ivp, bool widthSignificant, bool centered, out Matrix3x2 rotateM, out Matrix3x2 scaleM, out Matrix3x2 translateM)
        {
            rotateM = Matrix3x2.Rotation((float)(ivp.rotation * deg2rad), ImgCenter);
            float clientRatio = (float)ClientSize.Width / (float)ClientSize.Height;
            float ivpRatio = ivp.rect.Width / ivp.rect.Height;

            float zf = 0;

            if ((!widthSignificant || ivpRatio < clientRatio) && ivp.rect.Height != 0)
            {
                float scaley = (float)ClientSize.Height / (float)ImgSize.Height;
                float oldscaley = ivp.rect.Height;
                float zfy = scaley / oldscaley;
                zf = zfy;
            }
            else if (widthSignificant && ivpRatio > clientRatio && ivp.rect.Width != 0)
            {
                float scalex = (float)ClientSize.Width / (float)ImgSize.Width;
                float oldscalex = ivp.rect.Width;
                float zfx = scalex / oldscalex;
                zf = zfx;
            }
            else
            {
                //System.Windows.Forms.MessageBox.Show(string.Format("unidim ivp : {0}", ivp.ToString()));
                if (P.keepZoom) zf = _lastzf;
                else zf = 1;
            }
            scaleM = Matrix3x2.Scaling(zf, zf, Vector2.Zero);

            float xtrans = (ivp.l * ImgSize.Width) * zf;
            float ytrans = (ivp.t * ImgSize.Height) * zf;
            if (centered)
            {
                Matrix3x2 translation = Matrix3x2.Translation(-xtrans, -ytrans);
                Matrix3x2 prod = scaleM * translation;
                prod.Invert();
                Vector2 br = Matrix3x2.TransformPoint(prod, new Vector2(ClientSize.Width, ClientSize.Height));

                float w = (br.X - ivp.r * ImgSize.Width);
                float h = (br.Y - ivp.b * ImgSize.Height);
                xtrans = (ivp.l * ImgSize.Width - w / 2) * zf;
                ytrans = (ivp.t * ImgSize.Height - h / 2) * zf;
            }
            translateM = Matrix3x2.Translation(-xtrans, -ytrans);
        }
        #endregion

        #region Render
        protected override void OnRender(SharpDX.Direct2D1.DeviceContext dc)
        {
            if (_img != null)
            {
                Matrix3x2 prod = _rotation * _scale * _translation;
                dc.Transform = prod;

                RectangleF imageBounds = new RectangleF(0, 0, (int)(_img.Size.Width), (int)(_img.Size.Height));
                dc.DrawBitmap(_img, 1, InterpolationMode.Anisotropic, imageBounds, Matrix.Identity);
                dc.Transform = Matrix3x2.Identity;


                SaveIVP();


                //Draw zoom rectangle
                if (_doDrawZoom)
                {
                    var r = new RectangleF(_zoomRect.Left, _zoomRect.Top, _zoomRect.Width, _zoomRect.Height);
                    dc.DrawRectangle(r, _whiteBrush, 4);
                    dc.DrawRectangle(r, _blackBrush, 2);
                }

                //Draw scroll indicator
                if (P.showScroll)
                {
                    Ellipse inner = new Ellipse(_scrollIndicator, 2, 2);
                    Ellipse outer = new Ellipse(_scrollIndicator, 4, 4);
                    dc.DrawEllipse(inner, _whiteBrush, 5);
                    dc.DrawEllipse(outer, _blackBrush, 2);
                    //Draw Page turning indicator
                    if (_topHit || _bottomHit)
                    {
                        Ellipse tp = new Ellipse(_scrollIndicator, 2, 2);
                        var b = (_turnPageTimer >= DateTime.Now.AddMilliseconds(-500)) ? _redBrush : _greenBrush;
                        dc.DrawEllipse(tp, b, 2);
                    }
                }
                if (_freerot)
                {
                    //draw dot on the client center
                    Ellipse i = new Ellipse(ClientCenter, 2, 2);
                    Ellipse o = new Ellipse(ClientCenter, 4, 4);
                    dc.DrawEllipse(i, _whiteBrush, 5);
                    dc.DrawEllipse(o, _blackBrush, 2);
                    //_freerot = false;
                }
            }

            //Draw paging Info
            if (P.showPaging && _page?.Model != null)
            {
                string paging = string.Format("{0}/{1}", _page.PageNumber, _page.Model.Book.PageCount);


                TextLayout layout = new TextLayout(DirectWriteFactory, paging, _textFormat, 150, 25);
                if (layout != null)
                { 
                    layout.Draw(this._outlineTextRenderer, 0, 0);
                    layout.Dispose();
                }
            }

            if (_resizePreviewHPos > 0)
            {
                var start = new Vector2((int)_resizePreviewHPos -5, 0);
                var end = new Vector2((int)_resizePreviewHPos - 5, ClientSize.Height);
                dc.DrawLine(start, end, _greyBrush, 5f);
            }
        }


        /// <summary>
        ///-Adapts the matrices so the image is constrained to stay 
        ///inside the client and prevented to be zoomed into oblivion
        ///-Computes the scroll indicator position
        ///-Requests a redraw.
        /// </summary>
        protected void UpdateRenderParams(bool noInvalidate = false)
        {
            if (_img != null)
            {
                RectangleF imgRect;
                float radius = 6f;
                ConstraintTransforms(ref _rotation, ref _scale, ref _translation, out imgRect);

                if ((imgRect.Height <= ClientSize.Height) || !P.showScroll)
                {
                    float pos = _scrollingUp ? radius : ClientSize.Height - radius;
                    _scrollIndicator = new Vector2(ClientSize.Width - radius, pos);
                }
                else
                {
                    //position of the scrollIndicator
                    
                    float top = imgRect.Y + (ClientSize.Height / 2);                    //image top y coord
                    float bot = (imgRect.Y + imgRect.Height) - (ClientSize.Height / 2);   //image bottom y coord                   

                    //y position of the scroll indicator
                    float pos = radius + (((ClientCenter.Y - top) / (bot - top)) * (ClientSize.Height - radius));

                    if (pos < radius) pos = radius;
                    else if (pos > ClientSize.Height - radius) pos = ClientSize.Height - radius;

                    _scrollIndicator = new Vector2(ClientSize.Width - radius, pos);
                }

                RectangleF rect = new RectangleF(0, 0, _img.PixelSize.Width, _img.PixelSize.Height);
                TransformedBounds(imgRect, out rect, _scale);
            }
            if (!noInvalidate) Invalidate();
        }
        protected void ConstraintTransforms(ref Matrix3x2 rotateM, ref Matrix3x2 scaleM, ref Matrix3x2 translateM)
        {
            RectangleF dummy;
            ConstraintTransforms(ref rotateM, ref scaleM, ref translateM, out dummy);
        }
        protected void ConstraintTransforms(ref Matrix3x2 rotateM, ref Matrix3x2 scaleM, ref Matrix3x2 translateM, out RectangleF imgBoundingRect)
        {
            if (_img == null)
            {
                imgBoundingRect = new RectangleF();

                return;
            }


            // image and client widths and heights
            float clientW = (float)ClientSize.Width;
            float clientH = (float)ClientSize.Height;
            float imgW = (float)ImgSize.Width;
            float imgH = (float)ImgSize.Height;

            RectangleF imgRect = new RectangleF(0, 0, imgW, imgH);
            TransformedBounds(imgRect, out imgBoundingRect, _rotation);

            // min horizontal and vertical zoomfactors so that the image can't float inside the client frame
            // exception to that rule if the image size is smaller than the clientSize
            float minZoom, minHZoom, minVZoom;								//max zoom, max vertical zoom, max horizontal zoom
            minHZoom = clientW / imgBoundingRect.Width;
            minVZoom = clientH / imgBoundingRect.Height;

            minZoom = Math.Min(minHZoom, minVZoom);
            minZoom = Math.Min(minZoom, 1);

            // correcting zoom excesses if needed
            float curZoom = zoomf;
            if (curZoom < minZoom) scaleM = Matrix3x2.Scaling(minZoom, minZoom, clientCenterProj);

            Matrix3x2 transform = rotateM * scaleM * translateM;
            TransformedBounds(imgRect, out imgBoundingRect, transform);

            // horizontal centering
            if (imgBoundingRect.Width <= clientW)
            {
                float curcenter = imgBoundingRect.Left + (imgBoundingRect.Width / 2);
                float dstcenter = ClientCenter.X;

                Matrix3x2 m = Matrix3x2.Translation(dstcenter - curcenter, 0);
                translateM = translateM * m;
            }
            else
            {
                // do not allow image to get out of the view
                if (imgBoundingRect.Left > 0)
                {
                    Matrix3x2 m = Matrix3x2.Translation(-imgBoundingRect.Left, 0);
                    translateM = translateM * m;
                }
                else if (imgBoundingRect.Right < clientW)
                {
                    Matrix3x2 m = Matrix3x2.Translation(clientW - imgBoundingRect.Right, 0);
                    translateM = translateM * m;
                }
            }

            _bottomHit = _topHit = false;

            // vertical centering
            if (imgBoundingRect.Height <= clientH)
            {
                _bottomHit = _topHit = true;
                float curcenter = imgBoundingRect.Top + (imgBoundingRect.Height / 2);
                float dstcenter = ClientCenter.Y;
                Matrix3x2 m = Matrix3x2.Translation(0, dstcenter - curcenter);
                translateM = translateM * m;
            }
            else
            {
                // do not allow image to get out of the view
                if (imgBoundingRect.Top >= 0)
                {
                    Matrix3x2 m = Matrix3x2.Translation(0, -imgBoundingRect.Top);
                    translateM = translateM * m;
                    _topHit = true;
                }
                else if (imgBoundingRect.Bottom <= clientH)
                {
                    Matrix3x2 m = Matrix3x2.Translation(0, clientH - imgBoundingRect.Bottom);
                    translateM = translateM * m;
                    _bottomHit = true;
                }
            }

        }

        /// <summary>
        /// Get the bounding rect of rect transformed by transform
        /// works for any rotation
        /// </summary>
        /// <param name="rect">the rectangle to transform</param>
        /// <param name="boundingRect">the bounding rectangle output</param>
        /// <param name="transform">the transform to apply</param>
        private void TransformedBounds(RectangleF rect, out RectangleF boundingRect, Matrix3x2 transform)
        {
            Vector2 tl = Matrix3x2.TransformPoint(transform, rect.TopLeft);
            Vector2 tr = Matrix3x2.TransformPoint(transform, rect.TopRight);
            Vector2 bl = Matrix3x2.TransformPoint(transform, rect.BottomLeft);
            Vector2 br = Matrix3x2.TransformPoint(transform, rect.BottomRight);
            float t = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
            float b = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
            float l = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
            float r = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
            boundingRect = new RectangleF(l, t, (r - l), (b - t));
        }
        #endregion Render

        #region Transform Methods and properties
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        //extracts the rotation angle from the rotation matrix
        public float RotationAngle
        {
            get { return GetEulerAngle(ref _rotation); }
            set
            {
                if (this.RotationAngle != value)
                {
                    float rad = (float)((double)value * deg2rad);
                    _rotation = Matrix3x2.Rotation(rad, ImgCenter);
                    if (P.animRotation)
                    {
                        ImageViewingParams startIVP = _ivp;
                        UpdateRenderParams(true);
                        MakeIVP();
                        _anim.Init(startIVP, _ivp, OnAnimationTick, 300);
                    }
                    else UpdateRenderParams();

                    //ImageOrientationChanged(value);
                }
            }
        }

        public void Zoom(float scale, bool incremental, bool animate = false)
        {

            if (zoomf > maxZoom && scale > 0) return;
            if (incremental)                              // incremental zoom
            {
                scale = Math.Min(zoomf, 1) * 0.006f * scale;
                scale += 1;

            }
            //if (Single.IsInfinity(scale) || Single.IsNegativeInfinity(scale) || Single.IsNaN(scale))
            //{ System.Windows.Forms.MessageBox.Show("bad zoomfactor in zoom()"); }

            Matrix3x2 m;

            if (animate && P.animKeyZoom)
            {
                if (_anim.isRunning) _anim.Reset();
                ImageViewingParams startIVP = _ivp;
                m = Matrix3x2.Scaling(scale, scale, clientCenterProj);
                _scale = _scale * m;
                UpdateRenderParams(true);
                MakeIVP();

                _anim.Init(startIVP, _ivp, OnAnimationTickConstrained, 150);
                return;

            }

            if (_anim.isRunning) _anim.Reset();
            m = Matrix3x2.Scaling(scale, scale, clientCenterProj);
            _scale = _scale * m;

            UpdateRenderParams();
        }
        public bool IsDrawingZoomRect
        {
            get { return _doDrawZoom; }
            set { _drawingZoomRect = value; }//tricky pay attention
        }
        public void DrawZoomRect(int X, int Y)
        {
            if (!_doDrawZoom)
            {
                _zoomRect = new System.Drawing.Rectangle(X, Y, 0, 0);
                _doDrawZoom = true;
            }
            else
            {
                //X = _zoomRect.X;
                //Y -= _zoomRect.Y;
                _zoomRect.Width = X - _zoomRect.X;
                _zoomRect.Height = Y - _zoomRect.Y;
                Invalidate();
            }
        }
        public void ZoomRect(bool confirm = true)
        {

            _doDrawZoom = false;
            if (confirm)
            {
                _undostack.Push(_ivp);
                float top = Math.Min(_zoomRect.Top, _zoomRect.Bottom);
                float bottom = Math.Max(_zoomRect.Top, _zoomRect.Bottom);
                float left = Math.Min(_zoomRect.Left, _zoomRect.Right);
                float right = Math.Max(_zoomRect.Left, _zoomRect.Right);
                if (top == bottom) bottom += 1;
                if (left == right) right += 1;

                Vector2 tl = new Vector2(left, top);
                Vector2 br = new Vector2(right, bottom);

                Matrix3x2 prod = _scale * _translation;
                prod.Invert();
                tl = Matrix3x2.TransformPoint(prod, tl);
                br = Matrix3x2.TransformPoint(prod, br);
                if (P.animKeyZoom)
                {
                    ImageViewingParams end = new ImageViewingParams();
                    end.Set(tl.X / ImgSize.Width, tl.Y / ImgSize.Height, br.X / ImgSize.Width, br.Y / ImgSize.Height, RotationAngle);
                    Matrix3x2 rotateM, scaleM, translateM;
                    IvpToTransform(end, true, true, out rotateM, out scaleM, out translateM);
                    ConstraintTransforms(ref rotateM, ref scaleM, ref translateM);
                    MakeIVP(ref rotateM, ref scaleM, ref translateM, ref end);

                    _anim.Init(_ivp, end, OnAnimationTickConstrained, 150);
                }
                else
                {
                    _ivp.Set(tl.X / ImgSize.Width, tl.Y / ImgSize.Height, br.X / ImgSize.Width, br.Y / ImgSize.Height, RotationAngle);
                    IvpToTransform(_ivp, true, true, out _rotation, out _scale, out _translation);
                }
            }
            UpdateRenderParams();
        }
        /// <summary>
        /// Cancels the last zoomrect operation
        /// </summary>
        public void UndoZoomRect()
        {
            if (_undostack.Count > 0)
            {
                _ivp = _undostack.Pop();
                RestoreView();
                UpdateRenderParams();
            }
        }

        /// <summary>
        /// Rotate the image around the client center with the mouse cursor.
        /// </summary>
        /// <param name="x">mouse x</param>
        /// <param name="y">mouse y</param>
        /// <param name="init">set to true to initialize a new rotation</param>
        public void FreeRotation(float x, float y, bool init = false)
        {
            Vector2 mouse = new Vector2(x, y) - ClientCenter;
            Vector3 rotcursor = new Vector3(mouse.X, mouse.Y, 0);
            rotcursor.Normalize();


            if (init)
            {
                //get click vector and initial rotation
                _freeRotInitClick = rotcursor;
                _freeRotInitAngle = (float)-Math.Atan2((double)_rotation.M21, (double)_rotation.M11);

                //get center of rotation
                Matrix3x2 t = _rotation * _scale * _translation;
                t.Invert();
                _freeRotCenter = Matrix3x2.TransformPoint(t, ClientCenter);

                //adapt translation so that the center of rotation coincides with the client center
                Vector2 sc = Matrix3x2.TransformPoint(_scale, _freeRotCenter);
                _translation = Matrix3x2.Translation(ClientCenter - sc);
            }

            //get angle between current mouse pos and reference vector
            float dot = Vector3.Dot(rotcursor, _freeRotInitClick);
            float angle = (float)(Math.Acos(dot));
            if (Single.IsNaN(angle)) angle = 0;
            Vector3 cross = Vector3.Cross(rotcursor, _freeRotInitClick);
            if (cross.Z > 0) angle = (float)(2 * Math.PI) - angle;

            //add initial rotation to it
            angle += _freeRotInitAngle;



            //apply rotation
            _rotation = Matrix3x2.Rotation(angle, _freeRotCenter);
            _freerot = true;
            Invalidate();
        }
        public void Translate(float xTranslation, float yTranslation, bool turnpage, bool animate = false)
        {
            bool wasScrollingUp = _scrollingUp;
            _scrollingUp = (yTranslation > 0) ? true : false;
            if (_bottomHit && _topHit && wasScrollingUp != _scrollingUp) UpdateRenderParams();

            _doDrawZoom = false;
            if (turnpage && _bottomHit && yTranslation < 0)
            {
                _topHit = false;
                RequestNext();
                return;
            }
            else if (turnpage && _topHit && yTranslation > 0)
            {
                _bottomHit = false;
                RequestPrevious();
                return;
            }
            else if (_bottomHit || _topHit)
            {
                _turnPageTimer = DateTime.Now.AddYears(100);
            }

            if (animate && P.animScroll)
            {
                _scrollStartTime = DateTime.Now;
                _scrollStart.X = _translation.M31;
                _scrollStart.Y = _translation.M32;
                _scrollEnd.X += xTranslation;
                _scrollEnd.Y += yTranslation;

                if (_scrollTimer.Enabled == false)
                {
                    _scrollEnd.X += _scrollStart.X;
                    _scrollEnd.Y += _scrollStart.Y;
                    _scrollTimer.Start();
                }
            }
            else
            {
                Matrix3x2 m = Matrix3x2.Translation(xTranslation, yTranslation);
                
                _translation = _translation * m;
                UpdateRenderParams();
            }
        }

        
        private void ScrollTick(object sender, ElapsedEventArgs e)
        {
            
            double progress = ((DateTime.Now - _scrollStartTime).TotalMilliseconds / _scrollSpan.TotalMilliseconds);
            float x = 0, y = 0;
            if (progress < 1)
            {
                x = (float)(_scrollStart.X + (_scrollEnd.X - _scrollStart.X) * progress);
                y = (float)(_scrollStart.Y + (_scrollEnd.Y - _scrollStart.Y) * progress);
            }
            else
            {
                x = _scrollEnd.X;
                y = _scrollEnd.Y;
                _scrollTimer.Stop();
                _scrollStart = _scrollEnd = Vector2.Zero;
            }
            _syncontext.Post((o) => {
                _translation.M31 = x;
                _translation.M32 = y;
                UpdateRenderParams();
            }, null);
        }


        /// <summary>
        /// Reset all transformations to identity
        /// </summary>
        public void ResetView()
        {
            _translation = _rotation = _scale = Matrix3x2.Identity;
            _scale = Matrix3x2.Scaling(1f);
            UpdateRenderParams();
        }
        /// <summary>
        /// Set the scale matrix so the image fits within the client area
        /// </summary>
        public void Fit()
        {
            float xratio = (float)ClientSize.Width / (float)ImgSize.Width;
            float yratio = (float)ClientSize.Height / (float)ImgSize.Height;

            float zf = Math.Min(xratio, yratio);
            _scale = Matrix3x2.Scaling(zf, zf, ImgCenter);
            UpdateRenderParams();
        }
        /// <summary>
        /// Set the scale matrix so the image is the same width as the client
        /// </summary>
        public void FitW()
        {
            float xratio = (float)ClientSize.Width / (float)ImgSize.Width;

            _scale = Matrix3x2.Scaling(xratio, xratio, ImgCenter);
            UpdateRenderParams();
        }
        /// <summary>
        /// Set the scale matrix so the image is the same height as the client
        /// </summary>
        public void FitH()
        {
            float yratio = (float)ClientSize.Height / (float)ImgSize.Height;

            _scale = Matrix3x2.Scaling(yratio, yratio, ImgCenter);
            UpdateRenderParams();
        }



        public float GetEulerAngle(ref Matrix3x2 rotateM)
        {
            double a = -Math.Atan2((double)rotateM.M21, (double)rotateM.M11) * rad2deg;
            if (a < 0) a = 360 + a;
            return (float)a;
        }
        #endregion

        #region ivp
        protected void SaveIVP()
        {
            MakeIVP();
            if (P.rememberView && _page?.Model != null)
            {
                _ivp.filename = _page.Model.Filename;
                if (P.saveIvps) _ivp.isDirty = true;
                _page.Model.Ivp = _ivp;

            }
        }
        protected void MakeIVP()
        {
            MakeIVP(ref _rotation, ref _scale, ref _translation, ref _ivp);
        }

        protected void MakeIVP(ref Matrix3x2 rotateM, ref Matrix3x2 scaleM, ref Matrix3x2 translateM, ref ImageViewingParams ivp)
        {

            Matrix3x2 prod = scaleM * translateM;
            prod.Invert();

            Vector2 tl = Matrix3x2.TransformPoint(prod, Vector2.Zero);
            Vector2 br = Matrix3x2.TransformPoint(prod, new Vector2(ClientSize.Width, ClientSize.Height));


            ivp.Set(tl.X / ImgSize.Width, tl.Y / ImgSize.Height, br.X / ImgSize.Width, br.Y / ImgSize.Height, GetEulerAngle(ref rotateM));
        }
        #endregion

        #region animation handling

        //BblAnimationTickDelegates
        public bool OnAnimationTick(BblAnimation sender, ImageViewingParams ivp, bool animationEnd)
        {
            {
                if (ivp.isReset)
                    return true;

                _ivp = ivp;
                RestoreView();

                if (animationEnd) UpdateRenderParams();
                else Invalidate(); //KeepCentered();//

                return false;
                //if (animationEnd || (ivp.rotation % 90 == 0)) UpdateRenderParams();
                //else Invalidate();
            }
        }

        public bool OnAnimationTickConstrained(BblAnimation sender, ImageViewingParams ivp, bool animationEnd)
        {
            if (ivp.isReset)
                return true;
            _ivp = ivp;
            RestoreView();
            UpdateRenderParams();
            return false;
        }
        #endregion
    }
}
