using System.IO;
using SkiaSharp;
using Avalonia.Skia;
using Avalonia.Media.Imaging;

namespace Bubbles4.Services;

public class ThumbnailService
{
    public static  Bitmap LoadThumbnail(string imagePath, int maxWidth)
    {
        using var stream = File.OpenRead(imagePath);
        return Bitmap.DecodeToWidth(stream, maxWidth); // Automatically scales down during decode
    }
}