using System;
using System.IO;
using System.Threading;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Bubbles4.Services;

public static class ImageLoader
{
    public const int ThumbMaxSize = 200;
    public const int ImageMaxSize = 4000;

    public static Bitmap? LoadImage(string imagePath, int maxSize, CancellationToken token)
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
    
    public static Bitmap? DecodeImage(Stream stream, int maxSize, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            token.ThrowIfCancellationRequested();
            return Bitmap.DecodeToWidth(stream, maxSize); // Decode directly from stream
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    public static (int width, int height) GetTargetDimensions(int imageWidth, int imageHeight, int maxSize)
    {
        float scale =  Math.Min(1f, maxSize / (float)Math.Max(imageWidth, imageHeight));
        int targetWidth = (int)(imageWidth * scale);
        int targetHeight = (int)(imageHeight * scale);
        return (targetWidth, targetHeight);
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
    
    public static Bitmap ConvertSkiaBitmapToAvalonia(SKBitmap skBitmap)
    {
        using var image = SKImage.FromBitmap(skBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);

        // Copy SKData to a MemoryStream (which *is* seekable)
        var memoryStream = new MemoryStream();
        data.SaveTo(memoryStream);
        memoryStream.Position = 0;

        return new Bitmap(memoryStream);
    }
*/
  


  
}