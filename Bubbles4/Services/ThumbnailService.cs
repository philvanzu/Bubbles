using System;
using System.IO;
using SkiaSharp;
using Avalonia.Skia;
using Avalonia.Media.Imaging;
using Bubbles4.Models;

namespace Bubbles4.Services;

public class ThumbnailService
{
    public static  Bitmap LoadThumbnail(string imagePath, int maxWidth)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            return LoadThumbnail(stream, maxWidth); // Delegate to stream overload
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
    
    public static Bitmap LoadThumbnail(Stream stream, int maxWidth)
    {
        try
        {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            return Bitmap.DecodeToWidth(stream, maxWidth); // Decode directly from stream
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return null;
    }
}