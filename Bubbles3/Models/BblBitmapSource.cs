using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class BblBitmapSource : System.Windows.Media.Imaging.BitmapSource, IDisposable
{

    #region constructors
    public BblBitmapSource()
    {

    }

    public BblBitmapSource(string filePath)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));
        using(var fac = new ImagingFactory())
        { 
            using (var dec = new SharpDX.WIC.BitmapDecoder(fac, filePath, DecodeOptions.CacheOnDemand))
            {
                WICBitmapSource = dec.GetFrame(0);
            }
        }
    }

    GCHandle _pinnedArray;
    public BblBitmapSource(MemoryStream stream)
    {
        try
        {
            using(var fac = new ImagingFactory())
            { 
                _pinnedArray = GCHandle.Alloc(stream.GetBuffer(), GCHandleType.Pinned);
                IntPtr pointer = _pinnedArray.AddrOfPinnedObject();
                SharpDX.DataPointer p = new SharpDX.DataPointer(pointer, (int)stream.Length);
                using (WICStream wstream = new WICStream(fac, p))
                {
                    using (SharpDX.WIC.BitmapDecoder dec = new SharpDX.WIC.BitmapDecoder(fac, wstream, Guid.Empty, DecodeOptions.CacheOnDemand))
                    {
                        WICBitmapSource = dec.GetFrame(0);
                    }
                }
            }
        }
        catch(Exception e) { Console.WriteLine(e.Message); }
    }

    public BblBitmapSource(BitmapFrameDecode frame)
    {
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        WICBitmapSource = frame;
    }


    public BblBitmapSource(SharpDX.WIC.BitmapSource source)
    {
        WICBitmapSource = source;
    }
    #endregion

    /// <summary>
    /// Bitmap Data Source, Heart of the class
    /// </summary>
    public SharpDX.WIC.BitmapSource WICBitmapSource { get; }

    #region BitmapSource Interface Implementation
    public override int PixelWidth => WICBitmapSource.Size.Width;
    public override int PixelHeight => WICBitmapSource.Size.Height;
    public override double Height => PixelHeight;
    public override double Width => PixelWidth;

    public override double DpiX
    {
        get
        {
            WICBitmapSource.GetResolution(out double dpix, out double dpiy);
            return dpix;
        }
    }

    public override double DpiY
    {
        get
        {
            WICBitmapSource.GetResolution(out double dpix, out double dpiy);
            return dpiy;
        }
    }

    public override System.Windows.Media.PixelFormat Format
    {
        get
        {
            // this is a hack as PixelFormat is not public...
            // it would be better to do proper matching
            var ct = typeof(System.Windows.Media.PixelFormat).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Guid) },
                null);
            return (System.Windows.Media.PixelFormat)ct.Invoke(new object[] { WICBitmapSource.PixelFormat });
        }
    }

    // mostly for GIFs support (indexed palette of 256 colors)
    public override BitmapPalette Palette
    {
        get
        {
            using (var fac = new ImagingFactory())
            {
                var palette = new Palette(fac);
                try
                {
                    WICBitmapSource.CopyPalette(palette);
                }
                catch
                {
                    // no indexed palette (PNG, JPG, etc.)
                    // it's a pity SharpDX throws here,
                    // it would be better to return null more gracefully as this is not really an error
                    // if you only want to support non indexed palette images, just return null for the property w/o trying to get a palette
                    return null;
                }

                var list = new List<Color>();
                foreach (var c in palette.GetColors<int>())
                {
                    var bytes = BitConverter.GetBytes(c);
                    var color = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
                    list.Add(color);
                }
                return new BitmapPalette(list);
            }
            
        }
    }


    protected override Freezable CreateInstanceCore()
    {
        return new BblBitmapSource();
    }
    #endregion

    #region thumbnails
    public override void CopyPixels(Int32Rect sourceRect, Array pixels, int stride, int offset)
    {
        if (offset != 0)
            throw new NotSupportedException();

        WICBitmapSource.CopyPixels(
            new SharpDX.Mathematics.Interop.RawRectangle(sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height),
            (byte[])pixels, stride);
    }

    public System.Windows.Media.Imaging.BitmapSource GetThumbnail(System.Drawing.Size size)
    {
        double scale;
        //BblBitmapSource thumbnailSrc = null;
        //try
        //{
        //    BitmapFrameDecode frame = WICBitmapSource as BitmapFrameDecode;
        //    if (frame != null && frame.Thumbnail != null)
        //    {
        //        using (thumbnailSrc = new BblBitmapSource(frame.Thumbnail))
        //        {

        //            if (thumbnailSrc != null)
        //            {
        //                scale = (Math.Min(size.Width / (double)thumbnailSrc.PixelWidth, size.Height / (double)thumbnailSrc.PixelHeight));

        //                if (scale <= 1)
        //                {
        //                    var tb = Transform(thumbnailSrc, scale, 0);
        //                    //TODO find a way to compute the thumbnail in this thread without this weird double conversion.
        //                    // returning the TransformedBitmap directly results in heavy computations in the UI thread slowing the scrolling to a crawl.
        //                    using (var b = ConvertToBitmap(tb))
        //                        return ConvertToBitmapSource(b);
        //                }
        //            }
        //        }
        //    }
        //}
        //catch (SharpDX.SharpDXException) {;}
        //catch (Exception) {;}

        try
        { 
            scale = (Math.Min(size.Width / (double)PixelWidth, size.Height / (double)PixelHeight));
            if (scale < 1)
            {
                var tb = Transform(this, scale, 0);
                //TODO find a way to compute the thumbnail in this thread without this weird double conversion.
                // returning the TransformedBitmap directly results in heavy computations in the UI thread slowing the scrolling to a crawl.
                using (var b = ConvertToBitmap(tb))
                    return ConvertToBitmapSource(b);
            }
        }
        catch
        {
        
        }
        return null;
    }

    private static TransformedBitmap Transform( BblBitmapSource source, double scale, int angle)
    {
        // Set up the transformed thumbnail
        TransformedBitmap thumb = new TransformedBitmap();
        thumb.BeginInit();
        thumb.Source = source;
        TransformGroup transform = new TransformGroup();

        // Rotation
        if (Math.Abs(angle) % 360 != 0)
            transform.Children.Add(new RotateTransform(Math.Abs(angle)));

        // Scale
        if ((float)scale < 1.0f ) // Only downscale
        {
            double xScale = Math.Min(1.0, Math.Max(1.0 / (double)source.PixelWidth, scale));
            double yScale = Math.Min(1.0, Math.Max(1.0 / (double)source.PixelHeight, scale));

            if (angle < 0)
                xScale = -xScale;
            transform.Children.Add(new ScaleTransform(xScale, yScale));
        }
        thumb.Transform = transform;
        thumb.EndInit();
        return thumb;
    }
    #endregion

    public SharpDX.Direct2D1.Bitmap GetD2DBitmap(SharpDX.Direct2D1.DeviceContext dc, ImagingFactory factory)
    {
        
        using (var fc = new FormatConverter(factory))
        {
            
            fc.Initialize(WICBitmapSource, SharpDX.WIC.PixelFormat.Format32bppPBGRA,
                            SharpDX.WIC.BitmapDitherType.None, null, 0.0f,
                            SharpDX.WIC.BitmapPaletteType.Custom);

            var bmpProps = new SharpDX.Direct2D1.BitmapProperties(dc.PixelFormat , dc.Factory.DesktopDpi.Width, dc.Factory.DesktopDpi.Height);

            return SharpDX.Direct2D1.Bitmap1.FromWicBitmap(dc, fc, bmpProps);


        }
    }

    public static SharpDX.Direct2D1.Bitmap Test(SharpDX.Direct2D1.DeviceContext dc, ImagingFactory factory, System.Windows.Media.Imaging.BitmapSource src)
    {
        // PixelFormat settings/conversion
        if (src.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            // Convert BitmapSource
            FormatConvertedBitmap fcb = new FormatConvertedBitmap();
            fcb.BeginInit();
            fcb.Source = src;
            fcb.DestinationFormat = PixelFormats.Bgra32;
            fcb.EndInit();
            src = fcb;
        }

        SharpDX.Direct2D1.Bitmap retval = null;
        try
        {
            int stride = src.PixelWidth * (src.Format.BitsPerPixel + 7) / 8;
            int bufferSize = stride * src.PixelHeight;
            byte[] buffer = new byte[bufferSize];
            src.CopyPixels(Int32Rect.Empty, buffer, stride, 0);
            GCHandle pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();
            SharpDX.DataStream datastream = new SharpDX.DataStream(pointer, bufferSize, true, true);
            var bmpProps1 = new SharpDX.Direct2D1.BitmapProperties1(dc.PixelFormat, dc.Factory.DesktopDpi.Width, dc.Factory.DesktopDpi.Height);
            
            retval = new SharpDX.Direct2D1.Bitmap1(dc, new SharpDX.Size2(src.PixelWidth, src.PixelHeight), datastream, stride, bmpProps1);
            pinnedArray.Free();
        }
        catch (Exception e)
        {

        }
        return retval;
    }

    /// <summary>
    /// DISPOSE
    /// </summary>
    public void Dispose()
    {
        try
        {
            //ReleaseMips();
            WICBitmapSource.Dispose();
            if (_pinnedArray.IsAllocated) _pinnedArray.Free();
        }
        catch { }
    }
    //public override event EventHandler<ExceptionEventArgs> DecodeFailed;
    //public override event EventHandler DownloadCompleted;
    //public override event EventHandler<ExceptionEventArgs> DownloadFailed;
    //public override event EventHandler<DownloadProgressEventArgs> DownloadProgress;


    #region statics
    /// <summary>
    /// Converts BitmapSource to Bitmap.
    /// </summary>
    /// <param name="sourceWpf">BitmapSource</param>
    /// <returns>Bitmap</returns>
    private static System.Drawing.Bitmap ConvertToBitmap(System.Windows.Media.Imaging.BitmapSource sourceWpf)
    {
        System.Windows.Media.Imaging.BitmapSource bmpWpf = sourceWpf;

        // PixelFormat settings/conversion
        System.Drawing.Imaging.PixelFormat formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
        if (sourceWpf.Format == PixelFormats.Bgr24)
        {
            formatBmp = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
        }
        else if (sourceWpf.Format == System.Windows.Media.PixelFormats.Pbgra32)
        {
            formatBmp = System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
        }
        else if (sourceWpf.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            // Convert BitmapSource
            FormatConvertedBitmap convertWpf = new FormatConvertedBitmap();
            convertWpf.BeginInit();
            convertWpf.Source = sourceWpf;
            convertWpf.DestinationFormat = PixelFormats.Bgra32;
            convertWpf.EndInit();
            bmpWpf = convertWpf;
        }

        // Copy/Convert to Bitmap
        var bmp = new System.Drawing.Bitmap(bmpWpf.PixelWidth, bmpWpf.PixelHeight, formatBmp);
        System.Drawing.Rectangle rect = new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.WriteOnly, formatBmp);
        bmpWpf.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
        bmp.UnlockBits(data);

        return bmp;
    }
    private static System.Windows.Media.Imaging.BitmapSource FinalizeBitmapSource(System.Windows.Media.Imaging.BitmapSource source)
    {
        FormatConvertedBitmap retval = new FormatConvertedBitmap();
        retval.BeginInit();
        retval.Source = source;
        retval.DestinationFormat = PixelFormats.Bgra32;
        retval.EndInit();
        return retval;
    }

    public static System.Windows.Media.Imaging.BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bmp)
    {
        if (bmp == null) return null;
        IntPtr ip;
        try
        {
            ip = bmp.GetHbitmap();
        }
        catch
        {
            return null;
        }
        System.Windows.Media.Imaging.BitmapSource bs = null;
        try
        {
            bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(ip,
               IntPtr.Zero, System.Windows.Int32Rect.Empty,
               System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(ip);
        }

        return bs;
    }


    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);
    #endregion

    
}

