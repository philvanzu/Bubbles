using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Bubbles3.Models
{
    /// <summary>
    /// Holds a MemoryStream loaded with a compressed image file. 
    /// Pixel data array is not cached, decoded anew with every request.
    /// Used to get a Thumbnail in CachedBitmap form with pixel data held in memory for fast load in a <Image> Controls.
    /// or a Direct2D1 full sized Bitmap1 for use in the BblImageSurface Control. (must be disposed)
    /// Remember to Dispose to free the MemoryStream.
    /// </summary>
    public class BblImgSource : IDisposable
    {
        public MemoryStream Stream { get; private set; }
        public BitmapFrame Frame { get; private set; }

        public bool IsDisposed { get; private set; }
        public BblImgSource(MemoryStream stream)
        {
            if (stream == null) throw new ArgumentException();
            Stream = stream;
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                Frame = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                Frame.Freeze();
            }
            catch {
                Frame = null;
                Stream.Dispose();
            }
        }
        ~BblImgSource()
        {
            if (!IsDisposed) Dispose();
        }

        private static TransformedBitmap Scale(BitmapSource src, double scale)
        {
            // Set up the transformed thumbnail
            TransformedBitmap thumb = new TransformedBitmap();
            thumb.BeginInit();
            thumb.Source = src;
            TransformGroup transform = new TransformGroup();

            // Scale
            double xScale = Math.Min(1.0, Math.Max(1.0 / (double)src.PixelWidth, scale));
            double yScale = Math.Min(1.0, Math.Max(1.0 / (double)src.PixelHeight, scale));

            transform.Children.Add(new ScaleTransform(xScale, yScale));

            thumb.Transform = transform;
            thumb.EndInit();
            thumb.Freeze();
            return thumb;
        }

        public System.Windows.Media.Imaging.BitmapSource GetThumbnail(int width, int height)
        {
            BitmapSource thumb = null;

            // Try to read the thumbnail.
            if (Frame.Thumbnail != null)
            {
                try
                {
                    double scale;
                    scale = (Math.Min(width / (double)Frame.Thumbnail.PixelWidth, height / (double)Frame.Thumbnail.PixelHeight));
                    thumb = Scale(Frame.Thumbnail, scale);
                }
                catch { if (thumb != null) thumb = null; }
            }

            // Try to read the preview.
            if ( thumb == null && Frame.Decoder?.Preview != null)
            {
                try
                {
                    double scale;
                    var preview = Frame.Decoder.Preview;
                    scale = (Math.Min(width / (double)preview.PixelWidth, height / (double)preview.PixelHeight));
                    thumb = Scale(preview, scale);
                }
                catch { if (thumb != null) thumb = null; }
            }

            if (thumb == null)
            {
                double scale;
                scale = (Math.Min(width / (double)Frame.PixelWidth, height / (double)Frame.PixelHeight));
                thumb = Scale(Frame, scale);
            }

            var retval = new CachedBitmap(thumb, BitmapCreateOptions.None, BitmapCacheOption.Default);
            retval.Freeze();
            return retval;
        }

        const double _maxImageSize = 3840d;
        public SharpDX.Direct2D1.Bitmap GetD2DBitmap(SharpDX.Direct2D1.DeviceContext dc)
        {
            BitmapSource src = Frame;
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

            if(src.PixelHeight > _maxImageSize || src.PixelWidth > _maxImageSize)
            {
                double scale = (src.PixelWidth > src.PixelHeight) ? _maxImageSize / src.PixelWidth : _maxImageSize / src.PixelHeight;
                TransformedBitmap tb = new TransformedBitmap();
                tb.BeginInit();
                tb.Source = src;
                tb.Transform = new ScaleTransform(scale, scale);
                tb.EndInit();
                src = tb;
            }

            SharpDX.Direct2D1.Bitmap retval = null;
            GCHandle pinnedArray = GCHandle.Alloc(null);

            try
            {
                int stride = src.PixelWidth * (src.Format.BitsPerPixel + 7) / 8;
                int bufferSize = stride * src.PixelHeight;
                byte[] buffer = new byte[bufferSize];
                src.CopyPixels(System.Windows.Int32Rect.Empty, buffer, stride, 0);
                pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                using (SharpDX.DataStream datastream = new SharpDX.DataStream(pinnedArray.AddrOfPinnedObject(), bufferSize, true, false))
                { 
                    var bmpProps1 = new SharpDX.Direct2D1.BitmapProperties1(dc.PixelFormat, dc.Factory.DesktopDpi.Width, dc.Factory.DesktopDpi.Height);
                    retval = new SharpDX.Direct2D1.Bitmap1(dc, new SharpDX.Size2(src.PixelWidth, src.PixelHeight), datastream, stride, bmpProps1);
                }
            }
            catch
            {

            }
            finally
            {
                if(pinnedArray.IsAllocated) pinnedArray.Free();
            }
            return retval;
        }



        public void Dispose()
        {
            if (IsDisposed) return;
            if (Stream != null) Stream.Dispose();
            IsDisposed = true;
        }

    }
}
