using System;
using System.Collections.Generic;
using System.IO;

namespace Bubbles4.Models;

public static class FileTypes
{
    static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".rar", ".cbr", ".7z", ".zip", ".cbz", ".tar", ".tgz"
    };

    static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public static bool IsImage(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsArchive(string path)
    {
        return ArchiveExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsPdf(string path)
    {
        return PdfExtensions.Contains(Path.GetExtension(path));
    }
}