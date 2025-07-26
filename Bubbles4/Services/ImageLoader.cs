using System;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Metadata;
using SkiaSharp;
using Size = SixLabors.ImageSharp.Size;

namespace Bubbles4.Services;

public static class ImageLoader
{
    public const int ThumbMaxSize = 200;
    public const int ImageMaxSize = 4000;

    public static (Bitmap?, PixelSize?)? LoadImage(string imagePath, int maxSize, CancellationToken token)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            token.ThrowIfCancellationRequested();
            return DecodeImage(stream, maxSize, token);
        }
        catch (OperationCanceledException){}
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    
    public static (Bitmap?, PixelSize?)? DecodeImage(Stream stream, int maxSize, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
            PixelSize? pixelSize = null;
            using var codec = SKCodec.Create(stream);
            if (codec != null)
            {
                var info = codec.Info;
                pixelSize = new PixelSize(info.Width, info.Height);
            }
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
            token.ThrowIfCancellationRequested();
            return (Bitmap.DecodeToWidth(stream, maxSize), pixelSize); // Decode directly from stream
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
/*
    static Bitmap? DecodeImageFallback(Stream stream, int maxSize, CancellationToken token)
    {
        try
        {
            
            using var image = SixLabors.ImageSharp.Image.Load(
                DecoderOptions.Default,
                stream,
                out IImageFormat format);
            image.Mutate(x => 
                x.Resize(new ResizeOptions {
                Mode = ResizeMode.Max,
                Size = new Size(maxSize, maxSize)
            }));

            using var ms = new MemoryStream();
            image.SaveAsPng(ms); // or SaveAsJpeg
            ms.Position = 0;

            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
  */  
    public static (int width, int height) GetTargetDimensions(int imageWidth, int imageHeight, int maxSize)
    {
        float scale =  Math.Min(1f, maxSize / (float)Math.Max(imageWidth, imageHeight));
        int targetWidth = (int)(imageWidth * scale);
        int targetHeight = (int)(imageHeight * scale);
        return (targetWidth, targetHeight);
    }

    public static Stream? DecodeCropImage(Stream stream, int maxSize, Rect? cropRect)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var original = SKBitmap.Decode(stream);
            if (original == null)
                return null;

            SKBitmap sourceBitmap = original;

            // Crop if a valid cropRect is provided
            if (cropRect.HasValue)
            {
                var r = cropRect.Value;
                
                int left = (int)Math.Clamp(r.X, 0, original.Width - 1);
                int top = (int)Math.Clamp(r.Y, 0, original.Height - 1);
                int right = (int)Math.Clamp(r.X + r.Width, left + 1, original.Width);
                int bottom = (int)Math.Clamp(r.Y + r.Height, top + 1, original.Height);

                int width = right - left;
                int height = bottom - top;

                if (width <= 0 || height <= 0)
                    return null;

                var cropRectSkia = new SKRectI(left, top, left + width, top + height);

                var cropped = new SKBitmap(cropRectSkia.Width, cropRectSkia.Height);
                if (!original.ExtractSubset(cropped, cropRectSkia))
                    return null;

                sourceBitmap = cropped;
                
            }

            // Resize if necessary
            int imgW = sourceBitmap.Width;
            int imgH = sourceBitmap.Height;

            float scale = Math.Min(1f, maxSize / (float)Math.Max(imgW, imgH));
            int targetWidth = Math.Max(1, (int)(imgW * scale));
            int targetHeight = Math.Max(1, (int)(imgH * scale));

            SKBitmap finalBitmap = sourceBitmap;

            if (scale < 1f)
            {
                var resized = sourceBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
                if (resized == null)
                    return null;

                finalBitmap = resized;
            }
            
            return ConvertSkiaBitmapToStream(finalBitmap);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }

    public static Stream ConvertSkiaBitmapToStream(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);

        var memoryStream = new MemoryStream();
        data.SaveTo(memoryStream);
        memoryStream.Position = 0;

        return memoryStream;
    }


    public static void WriteStreamToFile(Stream stream, string filePath)
    {
        using var fileStream = File.Create(filePath);
        stream.CopyTo(fileStream);
    }

    /*
    public static Bitmap? DecodeImage(Stream stream, int maxSize)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            using var skbmp = SKBitmap.Decode(stream);
            if (skbmp == null) return null;
            int imgW = skbmp.Info.Width;
            int imgH = skbmp.Info.Height;

            float scale =  Math.Min(1f, maxSize / (float)Math.Max(imgW, imgH));
            int targetWidth = Math.Max(1, (int)(imgW * scale));
            int targetHeight = Math.Max(1, (int)(imgH * scale));

            if (scale < 1f)
            {
                var resized = skbmp.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
                if (resized == null) return null;
                return ConvertSkiaBitmapToAvalonia(resized);
            }
        }
        catch(Exception e){Console.WriteLine(e);}

        return null;
    }

*/
  


  
}