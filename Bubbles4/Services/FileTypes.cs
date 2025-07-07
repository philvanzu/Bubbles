using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    
    public static bool IsImageDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return false;

            return Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Any(IsImage);
        }
        catch
        {
            return false;
        }
    }
    
    public static bool IsDescendantPath(string parentPath, string childPath)
    {
        var parentUri = new Uri(AppendDirectorySeparator(parentPath), UriKind.Absolute);
        var childUri = new Uri(AppendDirectorySeparator(childPath), UriKind.Absolute);
        return parentUri.IsBaseOf(childUri);
    }
    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())
            ? path
            : path + System.IO.Path.DirectorySeparatorChar;
    }
    
    public static bool CouldBeDirectory(string path)
    {
        return string.IsNullOrEmpty(Path.GetExtension(path));
    }
    
    public static bool IsWatchable(string path)
    {
        if(IsImageDirectory(path) || IsArchive(path) || IsPdf(path) || IsImage(path))
            return true;
        
        if (CouldBeDirectory(path) && Directory.Exists(path)) 
            return true;
        
        return false;
    }
}