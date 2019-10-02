using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DXGI;

using SharpDX.DirectWrite;

using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using System.Windows;

namespace Bubbles3.Controls
{
    public abstract partial class D2D1Control : UserControl
    {
        bool _deviceIndependedResourcesCreated;



        SharpDX.Direct3D11.Device _d3dDevice;

        SharpDX.DXGI.SwapChain _swapChain;
        SharpDX.DXGI.Device _dxgiDevice;
        SharpDX.DXGI.Surface _surface;

        SharpDX.Direct2D1.Device _d2dDevice;
        SharpDX.Direct2D1.Factory _d2dFactory;
        SharpDX.Direct2D1.Bitmap1 _renderTarget;
        SharpDX.Direct2D1.DeviceContext _dc;

        

        public D2D1Control()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.UserPaint, true);
            InitializeComponent();
            
        }



        protected abstract void OnDispose();

        protected SharpDX.Direct2D1.Factory Direct2DFactory => _d2dFactory;





        protected SharpDX.Direct2D1.DeviceContext DC => _dc;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            var description = new SwapChainDescription()
            {
                BufferCount = 1,
                Usage = Usage.RenderTargetOutput,
                IsWindowed = true,
                ModeDescription = new ModeDescription(ClientSize.Width, ClientSize.Height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard,
                OutputHandle = Handle
            };

            SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.Debug | DeviceCreationFlags.BgraSupport, description, out _d3dDevice, out _swapChain);


            CreateDeviceIndependentResources();
            Invalidate();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_swapChain != null)
            {
                _swapChain.Dispose();
                _swapChain = null;
            }

            if (_d3dDevice != null)
            {
                if (_d3dDevice.ImmediateContext.Rasterizer.State != null) _d3dDevice.ImmediateContext.Rasterizer.State.Dispose();
                _d3dDevice.Dispose();
                _d3dDevice = null;
            }

            base.OnHandleDestroyed(e);
        }
        private void ReCreateDeviceIndependentResources()
        {
            CleanUpDeviceIndependentResources();
            CreateDeviceIndependentResources();
        }
        private void CreateDeviceIndependentResources()
        {
            _dxgiDevice = _d3dDevice.QueryInterface<SharpDX.DXGI.Device>();
            _d2dDevice = new SharpDX.Direct2D1.Device(_dxgiDevice);
            _d2dFactory = _d2dDevice.Factory;
            var dpiX = _d2dFactory.DesktopDpi.Width;
            var dpiY = _d2dFactory.DesktopDpi.Height;
            _dc = new SharpDX.Direct2D1.DeviceContext(_d2dDevice, DeviceContextOptions.None);
            _dc.PrimitiveBlend = PrimitiveBlend.SourceOver;

            var format = new SharpDX.Direct2D1.PixelFormat(Format.B8G8R8A8_UNorm, SharpDX.Direct2D1.AlphaMode.Premultiplied);
            var properties = new BitmapProperties1(format, dpiX, dpiY, BitmapOptions.Target | BitmapOptions.CannotDraw);
            _surface = _swapChain.GetBackBuffer<Surface>(0);
            _renderTarget = new Bitmap1(_dc, _surface, properties);
            
            _dc.Target = _renderTarget;
            _dc.AntialiasMode = AntialiasMode.PerPrimitive;

            OnCreateDeviceIndependentResources();
            this._deviceIndependedResourcesCreated = true;
        }

        private void CleanUpDeviceIndependentResources()
        {
            if (_deviceIndependedResourcesCreated) OnCleanUpDeviceIndependentResources();

            

            if (_surface != null && !_surface.IsDisposed) _surface.Dispose();
            _surface = null;

            if (_dc != null && !_dc.IsDisposed) _dc.Dispose();
            _dc = null;

            if (_d2dDevice != null && !_d2dDevice.IsDisposed) _d2dDevice.Dispose();
            _d2dDevice = null;

            if (_renderTarget != null && !_renderTarget.IsDisposed) _renderTarget.Dispose();
            _renderTarget = null;

            if (_d2dFactory != null && !_d2dFactory.IsDisposed) _d2dFactory.Dispose();
            _d2dFactory = null;
        }

        protected abstract void OnCleanUpDeviceIndependentResources();

        private void CleanUpDeviceResources()
        {
            OnCleanUpDeviceResources();
        }

        protected abstract void OnCleanUpDeviceResources();

        private void CreateDeviceResources()
        {
            OnCreateDeviceResources();
        }

        protected abstract void OnCreateDeviceResources();

        protected abstract void OnCreateDeviceIndependentResources();

        protected override void OnPaint(PaintEventArgs e)
        {
            CreateDeviceResources();
            try
            {
                this._dc.BeginDraw();
                try
                {
                    this._dc.Transform = Matrix3x2.Identity;
                    this._dc.Clear(new Color(this.BackColor.R, this.BackColor.G, this.BackColor.B));
                    OnRender(_dc);
                }
                finally
                {
                    try
                    {
                        this._dc.EndDraw();
                        _swapChain.Present(0, PresentFlags.None);
                    }
                    catch (SharpDX.SharpDXException ex)
                    {
                        if (ex.ResultCode == 0x8899000C) //D2DERR_RECREATE_TARGET
                        {
                            this.ReCreateDeviceIndependentResources();
                            Invalidate();
                        }
                    }
                }
            }
            finally
            {
                CleanUpDeviceResources();
            }
        }

        protected abstract void OnRender(SharpDX.Direct2D1.DeviceContext dc);

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // If the form is minimized, OnResize is triggered with a client-size of (0,0).
            if (ClientSize.IsEmpty)
                return;
            if (_swapChain == null)
                return;

            CleanUpDeviceIndependentResources();
            // Resize the back buffer.
            _swapChain.ResizeBuffers(1, ClientSize.Width, ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.AllowModeSwitch);

            CreateDeviceIndependentResources();
            Invalidate();
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            CleanUpDeviceResources();
            CleanUpDeviceIndependentResources();
            OnDispose();

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
            
        }
    }
}
